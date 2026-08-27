using System.Collections.Concurrent;
using OrbitMesh.Package;
using KasaTapoClient;

namespace OrbitMesh.TPLinkSmartHome;

/// <summary>
/// Discovers and controls TP-Link Kasa/Tapo devices on the local network via KasaTapoClient, which
/// implements the legacy XOR protocol, KLAP (Kasa's authenticated protocol required by 2023+ firmware -
/// e.g. KS205/KS225/KP125M) and TPAP (Tapo). LAN broadcast discovery runs on its own (longer) interval,
/// separate from the state-polling interval - the discovered hosts are reused for every state refresh
/// in between, the same discovery-based UX as Home Assistant's integration. KLAP/TPAP authentication
/// uses the TP-Link account email/password (see the Username/Password settings), not a separate local
/// credential.
/// </summary>
public sealed class TPLinkSmartHomePackage : IPackage
{
    private CancellationTokenSource? _cts;
    private volatile bool _forceDiscovery;

    // Hosts from the last discovery pass, reused for state polling in between passes.
    private List<string> _knownHosts = [];
    private DateTime _lastDiscoveryUtc = DateTime.MinValue;

    // Host per DeviceId from the last successful poll, so the on-demand control callbacks
    // (SetPower/SetChildPower) can reconnect directly instead of re-broadcasting on every call.
    private readonly Dictionary<string, string> _hostByDeviceId = new();

    // KasaDevice caches its transport's authenticated session internally (KlapTransport.EnsureHandshakeAsync
    // skips the handshake entirely while the session hasn't expired) - reconnecting from scratch on every
    // single call (the original design) was paying that handshake's full network round-trip cost (measured
    // ~3.2s against a real KS225) every single time instead of just once. Reusing the same KasaDevice
    // instance across both polling and on-demand commands is what actually gets latency down to what
    // Home Assistant's persistent-connection integration sees.
    private readonly ConcurrentDictionary<string, KasaDevice> _deviceCache = new();

    // These devices only tolerate one active session at a time - without this, a SetPower/SetChildPower
    // call landing while the polling loop is mid-poll on the same host (or two rapid taps) opens two
    // concurrent connections to the same physical device, which has been observed to throw
    // "Cannot access a disposed object: 'SemaphoreSlim'" from inside KasaTapoClient. Keyed by host so
    // different devices are never blocked by each other. This also now guards the shared cached
    // KasaDevice instance from concurrent use.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _hostLocks = new();

    private SemaphoreSlim GetHostLock(string host) => _hostLocks.GetOrAdd(host, _ => new SemaphoreSlim(1, 1));

    public static void Main(string[] args) => PackageHost.Start<TPLinkSmartHomePackage>(args);

    public void OnStart()
    {
        PackageHost.WriteInfo("Package starting - IsRunning: {0} - IsConnected: {1}", PackageHost.IsRunning, PackageHost.IsConnected);
        _cts = new CancellationTokenSource();
        _ = RunPollingLoopAsync(_cts.Token);
    }

    public void OnPreShutdown() => _cts?.Cancel();

    public void OnShutdown()
    {
        _cts?.Dispose();
        foreach (var device in _deviceCache.Values)
        {
            device.Dispose();
        }
        _deviceCache.Clear();
    }

    [MessageHandler(Description = "Forces a LAN discovery pass on the next polling tick instead of waiting for the discoveryIntervalMs setting to elapse.")]
    public void ForceDiscovery() => _forceDiscovery = true;

    private static DeviceCredentials GetCredentials() =>
        new(PackageHost.GetSettingValue<string>("Username"), PackageHost.GetSettingValue<string>("Password"));

    /// <summary>Returns the cached, already-authenticated connection for a host, connecting fresh only
    /// if there isn't one yet. Callers must hold that host's lock (<see cref="GetHostLock"/>) first.</summary>
    private async Task<KasaDevice> GetOrConnectDeviceAsync(string host, DeviceCredentials credentials)
    {
        if (_deviceCache.TryGetValue(host, out var cached))
        {
            return cached;
        }
        var device = await Discover.DiscoverSingleAsync(host, credentials: credentials);
        _deviceCache[host] = device;
        return device;
    }

    /// <summary>Runs <paramref name="operation"/> against the cached device for <paramref name="host"/>,
    /// transparently dropping and reconnecting once if it throws (the cached session can go stale if the
    /// device rebooted, its IP changed, or the network dropped). Callers must hold that host's lock first.</summary>
    private async Task WithDeviceAsync(string host, DeviceCredentials credentials, Func<KasaDevice, Task> operation)
    {
        var device = await GetOrConnectDeviceAsync(host, credentials);
        try
        {
            await operation(device);
        }
        catch (Exception)
        {
            EvictDevice(host, device);
            var fresh = await GetOrConnectDeviceAsync(host, credentials);
            await operation(fresh);
        }
    }

    private void EvictDevice(string host, KasaDevice device)
    {
        if (_deviceCache.TryGetValue(host, out var current) && ReferenceEquals(current, device))
        {
            _deviceCache.TryRemove(host, out _);
            device.Dispose();
        }
    }

    private async Task RunPollingLoopAsync(CancellationToken cancellationToken)
    {
        while (PackageHost.IsRunning && !cancellationToken.IsCancellationRequested)
        {
            var pollingInterval = PackageHost.GetSettingValue<int>("poolingInterval");
            var discoveryIntervalMs = PackageHost.GetSettingValue<int>("discoveryIntervalMs");
            var telemetryItemLifetime = Math.Max(pollingInterval * 2, 30000) / 1000;
            var credentials = GetCredentials();

            if (_forceDiscovery || _knownHosts.Count == 0 || (DateTime.UtcNow - _lastDiscoveryUtc).TotalMilliseconds >= discoveryIntervalMs)
            {
                _forceDiscovery = false;
                try
                {
                    var discovered = await Discover.DiscoverAsync(cancellationToken: cancellationToken);
                    PackageHost.WriteInfo("Discovered {0} TP-Link device(s) on the local network", discovered.Count);
                    var newHosts = discovered.Select(r => r.Host).ToList();
                    // A host that dropped out of discovery (device offline, DHCP lease changed, ...) should
                    // not keep a dangling cached connection around forever.
                    foreach (var goneHost in _knownHosts.Where(h => !newHosts.Contains(h)))
                    {
                        if (_deviceCache.TryRemove(goneHost, out var stale))
                        {
                            stale.Dispose();
                        }
                    }
                    _knownHosts = newHosts;
                    _lastDiscoveryUtc = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    PackageHost.WriteError("TP-Link discovery failed: {0}", ex.Message);
                }
            }

            // Devices are independent local-network hosts - poll them concurrently instead of one
            // at a time, so one slow/unreachable device doesn't delay every other device's refresh.
            var resolvedPerDevice = await Task.WhenAll(_knownHosts.Select(host => PollDeviceAsync(host, credentials, telemetryItemLifetime)));

            lock (_hostByDeviceId)
            {
                _hostByDeviceId.Clear();
                foreach (var (deviceId, host) in resolvedPerDevice.SelectMany(pairs => pairs))
                {
                    _hostByDeviceId[deviceId] = host;
                }
            }

            try
            {
                await Task.Delay(pollingInterval, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Polls one known host and returns the (DeviceId, Host) pairs it just reported - for a
    /// plain device that's a single entry; for a multi-outlet device, one entry per child.</summary>
    private async Task<List<(string DeviceId, string Host)>> PollDeviceAsync(string host, DeviceCredentials credentials, int telemetryItemLifetime)
    {
        var resolved = new List<(string, string)>();
        var hostLock = GetHostLock(host);
        await hostLock.WaitAsync();
        try
        {
            var device = await GetOrConnectDeviceAsync(host, credentials);
            try
            {
                await device.UpdateAsync();
            }
            catch (Exception)
            {
                EvictDevice(host, device);
                device = await GetOrConnectDeviceAsync(host, credentials);
                await device.UpdateAsync();
            }

            var systemInfo = device.SystemInfo;
            if (systemInfo == null)
            {
                PackageHost.WriteWarn("No system info returned for '{0}' ({1})", device.Alias, host);
                return resolved;
            }

            if (systemInfo.Children.Count > 0)
            {
                foreach (var child in systemInfo.Children)
                {
                    PackageHost.PushTelemetryItem($"TPLink-{host}-{child.Id}", new ChildOutletInformations
                    {
                        ParentDeviceId = systemInfo.DeviceId ?? string.Empty,
                        ChildId = child.Id,
                        Alias = child.Alias ?? string.Empty,
                        IsOn = child.IsOn ?? false
                    }, lifetime: telemetryItemLifetime);
                    resolved.Add((child.Id, host));
                }
                // SetChildPower's deviceId parameter is the PARENT device (the kiosk sends
                // ChildOutletInformations.ParentDeviceId, not ChildId, as that first argument) -
                // without this, only each child's own id resolved to a host, so every SetChildPower
                // call failed with "Unknown TP-Link device id" for the parent id it's actually given.
                if (!string.IsNullOrEmpty(systemInfo.DeviceId))
                {
                    resolved.Add((systemInfo.DeviceId, host));
                }
                return resolved;
            }

            // KasaTapoClient 1.2.4's standalone UpdateEnergyUsageAsync() is broken for SMART devices - it
            // always sends legacy-protocol emeter commands regardless of transport - but UpdateAsync()
            // above already populates EnergyUsage for a SMART/KLAP device with the energy_monitoring
            // component, so no separate call is needed here.
            object plugInfo = device.EnergyUsage != null
                ? PlugWithEnergyMeterInformations.CreateFrom(device)
                : PlugInformations.CreateFrom(device);
            PackageHost.PushTelemetryItem($"TPLink-{host}", plugInfo, lifetime: telemetryItemLifetime);
            if (!string.IsNullOrEmpty(systemInfo.DeviceId))
            {
                resolved.Add((systemInfo.DeviceId, host));
            }
        }
        catch (Exception ex)
        {
            PackageHost.WriteError("Unable to poll TP-Link device '{0}' : {1}", host, ex.Message);
        }
        finally
        {
            hostLock.Release();
        }
        return resolved;
    }

    [MessageHandler(Description = "Turns a Kasa/Tapo device on or off (deviceId from its last discovered PlugInformations telemetry item).")]
    public async Task SetPower(string deviceId, bool state)
    {
        var host = ResolveHost(deviceId);
        if (host == null)
        {
            PackageHost.WriteWarn("Unknown TP-Link device id '{0}' - has it been discovered yet ?", deviceId);
            return;
        }
        var total = System.Diagnostics.Stopwatch.StartNew();
        var hostLock = GetHostLock(host);
        await hostLock.WaitAsync();
        var lockWaitMs = total.ElapsedMilliseconds;
        try
        {
            var command = System.Diagnostics.Stopwatch.StartNew();
            await WithDeviceAsync(host, GetCredentials(), device => state ? device.TurnOnAsync() : device.TurnOffAsync());
            PackageHost.WriteInfo("SetPower({0}, {1}) timing - lockWait={2}ms command={3}ms total={4}ms",
                deviceId, state, lockWaitMs, command.ElapsedMilliseconds, total.ElapsedMilliseconds);
        }
        finally
        {
            hostLock.Release();
        }
    }

    [MessageHandler(Description = "Sets the brightness (0-100) of a dimmable device (deviceId from its last discovered PlugInformations telemetry item; Brightness must be non-null there).")]
    public async Task SetBrightness(string deviceId, int brightness)
    {
        var host = ResolveHost(deviceId);
        if (host == null)
        {
            PackageHost.WriteWarn("Unknown TP-Link device id '{0}' - has it been discovered yet ?", deviceId);
            return;
        }
        var total = System.Diagnostics.Stopwatch.StartNew();
        var hostLock = GetHostLock(host);
        await hostLock.WaitAsync();
        var lockWaitMs = total.ElapsedMilliseconds;
        try
        {
            var command = System.Diagnostics.Stopwatch.StartNew();
            await WithDeviceAsync(host, GetCredentials(), device =>
            {
                if (!device.Light.IsAvailable)
                {
                    PackageHost.WriteWarn("Device '{0}' ({1}) does not support brightness control", deviceId, device.Alias);
                    return Task.CompletedTask;
                }
                return device.Light.SetBrightnessAsync(brightness);
            });
            PackageHost.WriteInfo("SetBrightness({0}, {1}) timing - lockWait={2}ms command={3}ms total={4}ms",
                deviceId, brightness, lockWaitMs, command.ElapsedMilliseconds, total.ElapsedMilliseconds);
        }
        finally
        {
            hostLock.Release();
        }
    }

    [MessageHandler(Description = "Turns one outlet of a multi-outlet device (e.g. the KP400 power strip) on or off.")]
    public async Task SetChildPower(string deviceId, string childId, bool state)
    {
        var host = ResolveHost(deviceId);
        if (host == null)
        {
            PackageHost.WriteWarn("Unknown TP-Link device id '{0}' - has it been discovered yet ?", deviceId);
            return;
        }
        var total = System.Diagnostics.Stopwatch.StartNew();
        var hostLock = GetHostLock(host);
        await hostLock.WaitAsync();
        var lockWaitMs = total.ElapsedMilliseconds;
        try
        {
            var command = System.Diagnostics.Stopwatch.StartNew();
            await WithDeviceAsync(host, GetCredentials(), device => state ? device.TurnChildOnAsync(childId) : device.TurnChildOffAsync(childId));
            PackageHost.WriteInfo("SetChildPower({0}, {1}, {2}) timing - lockWait={3}ms command={4}ms total={5}ms",
                deviceId, childId, state, lockWaitMs, command.ElapsedMilliseconds, total.ElapsedMilliseconds);
        }
        finally
        {
            hostLock.Release();
        }
    }

    private string? ResolveHost(string deviceId)
    {
        lock (_hostByDeviceId)
        {
            return _hostByDeviceId.GetValueOrDefault(deviceId);
        }
    }
}
