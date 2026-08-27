using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using OrbitMesh.Package;

namespace OrbitMesh.NetworkTools;

/// <summary>
/// Network diagnostics/monitoring package: ping, TCP port check/scan, HTTP check, Wake-on-LAN, DNS lookup.
/// Everything here is standard .NET networking (no third-party API), so nothing to verify against an
/// external service - it's cross-platform out of the box on .NET.
/// </summary>
public sealed class NetworkToolsPackage : IPackage
{
    private const int DefaultCheckIntervalSeconds = 60;
    private static readonly HttpClient HttpClient = new();

    private readonly Dictionary<string, DateTime> _monitoringChecks = new();
    private List<MonitoringResource> _resources = [];
    private CancellationTokenSource? _cts;

    public static void Main(string[] args) => PackageHost.Start<NetworkToolsPackage>(args);

    public void OnStart()
    {
        _cts = new CancellationTokenSource();
        RefreshResources();
        PackageHost.SettingsUpdated += (_, _) => RefreshResources();
        _ = RunMonitoringLoopAsync(_cts.Token);
    }

    public void OnPreShutdown() => _cts?.Cancel();

    public void OnShutdown() => _cts?.Dispose();

    // Re-parses the "Monitoring" JSON setting only when it actually changes, instead of on every
    // 1-second tick of the loop below.
    private void RefreshResources() =>
        _resources = PackageHost.ContainsSetting("Monitoring") ? PackageHost.GetSettingAsJson<List<MonitoringResource>>("Monitoring") ?? [] : [];

    private async Task RunMonitoringLoopAsync(CancellationToken cancellationToken)
    {
        while (PackageHost.IsRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var due = _resources.Where(r =>
                    !_monitoringChecks.TryGetValue(r.Name, out var last) || DateTime.Now.Subtract(last).TotalSeconds >= (r.Interval ?? DefaultCheckIntervalSeconds)).ToList();
                // Mark as checked before awaiting (not after) so a slow check can't make its own
                // resource look "due" again on the next tick while it's still in flight.
                foreach (var resource in due)
                {
                    _monitoringChecks[resource.Name] = DateTime.Now;
                }
                // Resources are independent (different hosts/URLs) - check them concurrently instead
                // of serializing every due resource behind the slowest one.
                await Task.WhenAll(due.Select(CheckResourceAsync));
            }
            catch (Exception ex)
            {
                PackageHost.WriteError("Monitor task error : " + ex.Message);
            }
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    [MessageHandler(Description = "Pings the specified host and returns the response time in ms (-1 if unreachable).")]
    public long Ping(string host, int timeout = 5000)
    {
        var reply = PingSafe(host, timeout);
        if (reply is { Status: IPStatus.Success })
        {
            PackageHost.WriteInfo("Reply from {0}: bytes={1} time={2}ms TTL={3}", host, reply.Buffer.Length, reply.RoundtripTime, reply.Options?.Ttl ?? 0);
            return reply.RoundtripTime;
        }
        PackageHost.WriteInfo("Request to {0} failed : {1}", host, reply?.Status.ToString() ?? "Unknown");
        return -1;
    }

    [MessageHandler(Description = "Checks a TCP port's reachability and returns the response time in ms (-1 if closed/unreachable).")]
    public long CheckPort(string host, int port, int timeout = 5000)
    {
        try
        {
            using var tcpClient = new TcpClient();
            var sw = Stopwatch.StartNew();
            var connectTask = tcpClient.ConnectAsync(host, port);
            if (connectTask.Wait(timeout) && tcpClient.Connected)
            {
                sw.Stop();
                return sw.ElapsedMilliseconds;
            }
            return -1;
        }
        catch
        {
            return -1;
        }
    }

    [MessageHandler(Description = "Checks an HTTP address and returns the response time in ms (-1 on failure).")]
    public async Task<long> CheckHttp(string address, int timeout = 100000)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            var sw = Stopwatch.StartNew();
            await HttpClient.GetStringAsync(address, cts.Token);
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }
        catch
        {
            return -1;
        }
    }

    [MessageHandler(Description = "Scans a TCP port range on a host and returns which ports are open.")]
    public Dictionary<int, bool> ScanPort(string host, int startPort, int endPort, int timeout = 100)
    {
        var result = new ConcurrentDictionary<int, bool>();
        Parallel.For(startPort, endPort + 1, port => result[port] = CheckPort(host, port, timeout) >= 0);
        return result.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    [MessageHandler(Description = "Sends a Wake-on-LAN magic packet to the given MAC address.")]
    public bool WakeUp(string macAddress)
    {
        try
        {
            var mac = PhysicalAddress.Parse(macAddress.Replace(":", "-").ToUpperInvariant()).GetAddressBytes();
            var packet = new byte[6 + 16 * mac.Length];
            for (var i = 0; i < 6; i++)
            {
                packet[i] = 0xFF;
            }
            for (var i = 0; i < 16; i++)
            {
                mac.CopyTo(packet, 6 + i * mac.Length);
            }
            using var client = new UdpClient { EnableBroadcast = true };
            client.Send(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 9));
            return true;
        }
        catch (Exception ex)
        {
            PackageHost.WriteError("Error while waking up the host with MAC '{0}' : {1}", macAddress, ex.Message);
            return false;
        }
    }

    [MessageHandler(Description = "Resolves a host name to its IP addresses.")]
    public string[] DnsLookup(string host)
    {
        try
        {
            return Dns.GetHostAddresses(host).Select(i => i.ToString()).ToArray();
        }
        catch
        {
            return [];
        }
    }

    private async Task CheckResourceAsync(MonitoringResource resource)
    {
        try
        {
            long result = -1;
            bool? state = null;
            var metadatas = new Dictionary<string, object> { ["Type"] = resource.Type };

            switch (resource.Type.ToLowerInvariant())
            {
                case "ping":
                    metadatas["Hostname"] = resource.Hostname ?? string.Empty;
                    var reply = PingSafe(resource.Hostname ?? string.Empty);
                    if (reply is { Status: IPStatus.Success })
                    {
                        result = reply.RoundtripTime;
                    }
                    break;
                case "tcp":
                    metadatas["Hostname"] = resource.Hostname ?? string.Empty;
                    metadatas["Port"] = resource.Port ?? 0;
                    result = CheckPort(resource.Hostname ?? string.Empty, resource.Port ?? 0);
                    break;
                case "http":
                    metadatas["Address"] = resource.Address ?? string.Empty;
                    try
                    {
                        using var cts = new CancellationTokenSource(resource.Timeout ?? 100000);
                        var sw = Stopwatch.StartNew();
                        var response = await HttpClient.GetStringAsync(resource.Address, cts.Token);
                        sw.Stop();
                        result = sw.ElapsedMilliseconds;
                        if (resource.Regex != null)
                        {
                            metadatas["Regex"] = resource.Regex;
                            state = Regex.IsMatch(response, resource.Regex);
                        }
                    }
                    catch
                    {
                        // result stays -1
                    }
                    break;
                default:
                    PackageHost.WriteWarn("Unknown monitoring type : " + resource.Type);
                    return;
            }

            PackageHost.PushTelemetryItem(resource.Name, new MonitoringResult { ResponseTime = result, State = state ?? result >= 0 }, metadatas: metadatas);
        }
        catch (Exception ex)
        {
            PackageHost.WriteError("Unable to monitor '{0}' ({1}) : {2}", resource.Name, resource.Type, ex.Message);
        }
    }

    private static PingReply? PingSafe(string hostNameOrAddress, int timeout = -1)
    {
        try
        {
            using var pingSender = new Ping();
            return timeout > 0 ? pingSender.Send(hostNameOrAddress, timeout) : pingSender.Send(hostNameOrAddress);
        }
        catch
        {
            return null;
        }
    }
}
