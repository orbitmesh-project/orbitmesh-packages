using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using OrbitMesh.Package;
using SharpOnvifClient;
using SharpOnvifClient.Events;
using SharpOnvifClient.Security;

namespace OrbitMesh.OnvifDoods;

/// <summary>
/// Watches one or more ONVIF cameras for motion and runs a snapshot through a DOODS2
/// (https://github.com/snowzach/doods2) object-detection server whenever motion fires, publishing
/// the result as a Telemetry Item. One instance handles every camera in the "Cameras" setting rather
/// than one instance per camera - each OrbitMesh package instance is its own .NET process (~45-65MB
/// baseline just for the runtime, see any package's own RAM in the Console's Packages page), so N
/// cameras as N instances would pay that baseline N times over for what is, per camera, a handful of
/// async ONVIF/HTTP calls. One instance instead runs one event-subscription task per camera
/// concurrently in-process (see RunCameraAsync) - same pattern Waze already uses for its "Places"/
/// "Trips" lists.
///
/// ONVIF motion events are the primary trigger (PullPoint subscription, the same approach the
/// SharpOnvif sample client uses); a per-camera PollingIntervalSeconds setting can additionally force
/// a detection on a fixed timer, for something a motion event would never fire for. A per-camera
/// MinSecondsBetweenDetections cooldown keeps continuous motion (someone standing in frame) from
/// spamming DOODS2/Telemetry with one detection per event.
/// </summary>
public sealed class OnvifDoodsPackage : IPackage
{
    private static readonly HttpClient DoodsHttpClient = new();

    private CancellationTokenSource? _cts;

    // One authenticated HttpClient per camera, reused across detections - a fresh HttpClientHandler
    // per call would mean re-negotiating the Digest/Basic challenge (an extra round-trip) every time.
    private readonly ConcurrentDictionary<string, HttpClient> _snapshotHttpClients = new();

    private readonly ConcurrentDictionary<string, DateTime> _lastDetectionUtcByCamera = new();

    public static void Main(string[] args) => PackageHost.Start<OnvifDoodsPackage>(args);

    public void OnStart()
    {
        PackageHost.WriteInfo("Package starting - IsRunning: {0} - IsConnected: {1}", PackageHost.IsRunning, PackageHost.IsConnected);
        _cts = new CancellationTokenSource();
        foreach (var camera in GetCameras())
        {
            _ = RunCameraAsync(camera, _cts.Token);
        }
    }

    public void OnPreShutdown() => _cts?.Cancel();

    public void OnShutdown()
    {
        _cts?.Dispose();
        foreach (var client in _snapshotHttpClients.Values)
        {
            client.Dispose();
        }
        _snapshotHttpClients.Clear();
    }

    [MessageHandler(Description = "Takes a snapshot from a configured camera right now, runs it through DOODS2 and returns/publishes the result. Bypasses MinSecondsBetweenDetections. includeImage attaches the captured JPEG (base64) to the result and its published Telemetry Item - off by default since an image is a lot heavier than everything else this package publishes.")]
    public Task<DetectionResult> DetectNow(string cameraName, bool includeImage = false)
    {
        var camera = GetCameras().FirstOrDefault(c => c.Name.Equals(cameraName, StringComparison.OrdinalIgnoreCase));
        if (camera == null)
        {
            PackageHost.WriteWarn("Unknown camera '{0}' - check the \"Cameras\" setting", cameraName);
            return Task.FromResult(new DetectionResult { CameraName = cameraName, DetectedAtUtc = DateTime.UtcNow });
        }
        return RunDetectionAsync(camera, ignoreCooldown: true, includeImage: includeImage);
    }

    [MessageHandler(Description = "Lists the camera names configured in the \"Cameras\" setting.")]
    public List<string> GetCameraNames() => GetCameras().Select(c => c.Name).ToList();

    private static List<CameraSetting> GetCameras() => PackageHost.GetSettingAsJson<List<CameraSetting>>("Cameras") ?? [];

    private static Dictionary<string, double> GetLabels() =>
        PackageHost.GetSettingAsJson<Dictionary<string, double>>("Labels") ?? new Dictionary<string, double> { ["person"] = 60 };

    /// <summary>Keeps one camera's ONVIF motion-event subscription alive for as long as the package
    /// runs, resubscribing from scratch (rather than retrying the same dead reference) whenever a pull
    /// fails - the subscription itself, or the camera's connection, can go stale on a reboot/network
    /// blip without the process itself throwing until the next pull attempt.</summary>
    private async Task RunCameraAsync(CameraSetting camera, CancellationToken cancellationToken)
    {
        var pollingTask = camera.PollingIntervalSeconds > 0
            ? RunPollingAsync(camera, cancellationToken)
            : Task.CompletedTask;

        if (!camera.EnableMotionEvents)
        {
            // Confirmed against real hardware (via ONVIF Device Manager, independent of this package)
            // that some cameras never fire a single ONVIF motion event despite accepting the PullPoint
            // subscription - EnableMotionEvents=false skips that subscription/pull loop entirely for
            // such a camera instead of polling it forever for something that will never arrive.
            PackageHost.WriteInfo("Camera '{0}': ONVIF motion events disabled (EnableMotionEvents=false) - relying on PollingIntervalSeconds only", camera.Name);
            await pollingTask;
            return;
        }

        while (PackageHost.IsRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await WatchMotionEventsAsync(camera, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                PackageHost.WriteError("Camera '{0}': ONVIF event subscription failed, retrying in 30s: {1}", camera.Name, ex.Message);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        await pollingTask;
    }

    private async Task WatchMotionEventsAsync(CameraSetting camera, CancellationToken cancellationToken)
    {
        var authentication = new DigestAuthenticationSchemeOptions(DigestAuthentication.HttpDigest | DigestAuthentication.WsUsernameToken);
        using var client = new SimpleOnvifClient(camera.OnvifUri, camera.Username, camera.Password, authentication, disableExpect100Continue: true);

        var subscription = await client.PullPointSubscribeAsync(60);
        var subscriptionAddress = subscription.SubscriptionReference.Address.Value;
        PackageHost.WriteInfo("Camera '{0}': subscribed to ONVIF motion events", camera.Name);

        // PullPointPullMessagesAsync's timeoutInSeconds asks the camera to hold the HTTP response open
        // (long-poll) until either a message arrives or the timeout elapses - but that's a request, not
        // a guarantee. A camera whose ONVIF Events stack doesn't actually implement long-polling just
        // answers immediately with zero messages every time, which turned a 30s-per-iteration loop into
        // thousands of empty SOAP calls per minute (observed against a real camera - not hypothetical).
        // MinPullInterval floors the loop's cadence independently of whatever the camera actually does,
        // so a broken long-poll degrades to "polls every 2s" instead of "hammers the camera as fast as
        // the network allows".
        var minPullInterval = TimeSpan.FromSeconds(2);
        var emptyPullsInARow = 0;
        var lastHeartbeatUtc = DateTime.UtcNow;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var pullStartedUtc = DateTime.UtcNow;
                PullMessagesResponse messages;
                try
                {
                    messages = await client.PullPointPullMessagesAsync(subscriptionAddress, timeoutInSeconds: 30, maxMessages: 100);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    PackageHost.WriteWarn("Camera '{0}': pull-point subscription went stale ({1}) - resubscribing", camera.Name, ex.Message);
                    subscription = await client.PullPointSubscribeAsync(60);
                    subscriptionAddress = subscription.SubscriptionReference.Address.Value;
                    continue;
                }

                var elapsed = DateTime.UtcNow - pullStartedUtc;
                if (elapsed < minPullInterval)
                {
                    try
                    {
                        await Task.Delay(minPullInterval - elapsed, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                var received = messages.NotificationMessage ?? [];
                if (received.Length == 0)
                {
                    emptyPullsInARow++;
                    // One heartbeat roughly every 5 minutes, by wall clock rather than pull count - the
                    // pull rate itself varies with whether this camera's long-poll actually works.
                    if (DateTime.UtcNow - lastHeartbeatUtc >= TimeSpan.FromMinutes(5))
                    {
                        PackageHost.WriteInfo("Camera '{0}': still listening for ONVIF events - none received in the last {1} pull(s)", camera.Name, emptyPullsInARow);
                        lastHeartbeatUtc = DateTime.UtcNow;
                    }
                }
                else
                {
                    emptyPullsInARow = 0;
                }

                foreach (var notification in received)
                {
                    if (OnvifEvents.IsMotionDetected(notification) == true)
                    {
                        _ = RunTriggeredDetectionAsync(camera);
                    }
                    else
                    {
                        // Logged regardless of recognition: cheap/OEM ONVIF firmwares often use a
                        // non-standard topic for their motion event, which OnvifEvents.IsMotionDetected
                        // (RuleEngine/CellMotionDetector/Motion, RuleEngine/MotionRegionDetector/Motion,
                        // VideoSource/MotionAlarm) won't recognize - seeing the raw topic here is the way
                        // to tell "camera never sends anything" apart from "sends it, wrong topic".
                        PackageHost.WriteInfo("Camera '{0}': received unrecognized ONVIF event, topic='{1}'", camera.Name, GetTopic(notification));
                    }
                }
            }
        }
        finally
        {
            try
            {
                await client.PullPointUnsubscribeAsync(subscriptionAddress);
            }
            catch
            {
                // Best-effort - the subscription expires on its own (initialTerminationTimeInSeconds)
                // even if this camera is unreachable right when we try to clean up after ourselves.
            }
        }
    }

    /// <summary>Same extraction OnvifEvents.IsMotionDetected uses internally, exposed here purely for
    /// diagnostic logging of events it doesn't recognize.</summary>
    private static string GetTopic(NotificationMessageHolderType message)
    {
        try
        {
            return message.Topic?.Any?.FirstOrDefault()?.Value ?? "(no topic)";
        }
        catch (Exception ex)
        {
            return $"(unreadable: {ex.Message})";
        }
    }

    private async Task RunPollingAsync(CameraSetting camera, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(camera.PollingIntervalSeconds, 5)), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            await RunTriggeredDetectionAsync(camera);
        }
    }

    /// <summary>Entry point for both the ONVIF motion path and the polling path - the only two callers
    /// that must respect MinSecondsBetweenDetections (DetectNow is an explicit ask and always runs).</summary>
    private async Task RunTriggeredDetectionAsync(CameraSetting camera)
    {
        try
        {
            await RunDetectionAsync(camera, ignoreCooldown: false);
        }
        catch (Exception ex)
        {
            PackageHost.WriteError("Camera '{0}': detection failed: {1}", camera.Name, ex.Message);
        }
    }

    private async Task<DetectionResult> RunDetectionAsync(CameraSetting camera, bool ignoreCooldown, bool includeImage = false)
    {
        if (!ignoreCooldown &&
            _lastDetectionUtcByCamera.TryGetValue(camera.Name, out var last) &&
            (DateTime.UtcNow - last).TotalSeconds < camera.MinSecondsBetweenDetections)
        {
            return new DetectionResult { CameraName = camera.Name, DetectedAtUtc = last };
        }

        var jpegBytes = await CaptureSnapshotAsync(camera);
        PackageHost.WriteInfo("Camera '{0}': captured a {1} KB snapshot via {2}", camera.Name, jpegBytes.Length / 1024, string.IsNullOrEmpty(camera.RtspUrl) ? "ONVIF" : "RTSP");

        List<Detection> detections;
        var doodsUrl = PackageHost.GetSettingValue<string>("DoodsUrl") ?? "";
        try
        {
            var doods = new DoodsClient(DoodsHttpClient, doodsUrl);
            var detectorName = PackageHost.GetSettingValue<string>("DoodsDetectorName") is { Length: > 0 } d ? d : "default";
            detections = await doods.DetectAsync(jpegBytes, detectorName, GetLabels(), CancellationToken.None);
        }
        catch (Exception ex)
        {
            PackageHost.WriteError("Camera '{0}': DOODS2 call failed ({1}): {2}{3}",
                camera.Name, doodsUrl, ex.Message, ex.InnerException != null ? " - " + ex.InnerException.Message : "");
            throw;
        }

        var result = new DetectionResult
        {
            CameraName = camera.Name,
            DetectedAtUtc = DateTime.UtcNow,
            Detections = detections,
            ImageBase64 = includeImage ? Convert.ToBase64String(jpegBytes) : null
        };
        _lastDetectionUtcByCamera[camera.Name] = result.DetectedAtUtc;
        PackageHost.PushTelemetryItem(camera.Name, result, lifetime: Math.Max(camera.MinSecondsBetweenDetections * 2, 60));

        PackageHost.WriteInfo("Camera '{0}': {1}", camera.Name, detections.Count > 0
            ? "detected " + string.Join(", ", detections.Select(d => $"{d.Label} ({d.Confidence:0}%)"))
            : "no matching object in this frame");

        return result;
    }

    /// <summary>Grabs one frame to feed DOODS2 - via ONVIF's own snapshot service by default, or via
    /// RTSP+ffmpeg (<see cref="CameraSetting.RtspUrl"/>) for a camera whose ONVIF snapshot service
    /// doesn't actually work (confirmed against a real camera: `GetSnapshotUriAsync` resolves cleanly,
    /// but the URL it returns refuses every request with zero bytes back, auth strategy and HTTP
    /// version both irrelevant - not every camera claiming ONVIF compliance has a working
    /// implementation of every service it advertises). ONVIF stays the default because it needs no
    /// extra dependency and works fine on cameras with a proper implementation.</summary>
    private async Task<byte[]> CaptureSnapshotAsync(CameraSetting camera) =>
        string.IsNullOrEmpty(camera.RtspUrl)
            ? await CaptureOnvifSnapshotAsync(camera)
            : await CaptureRtspSnapshotAsync(camera);

    private async Task<byte[]> CaptureOnvifSnapshotAsync(CameraSetting camera)
    {
        var authentication = new DigestAuthenticationSchemeOptions(DigestAuthentication.HttpDigest | DigestAuthentication.WsUsernameToken);
        using var onvif = new SimpleOnvifClient(camera.OnvifUri, camera.Username, camera.Password, authentication, disableExpect100Continue: true);

        string profileToken;
        string snapshotUri;
        try
        {
            profileToken = camera.ProfileToken;
            if (string.IsNullOrEmpty(profileToken))
            {
                var profiles = await onvif.GetProfilesAsync();
                profileToken = profiles.Profiles.First().token;
            }
            var snapshot = await onvif.GetSnapshotUriAsync(profileToken);
            snapshotUri = snapshot.Uri;
        }
        catch (Exception ex)
        {
            PackageHost.WriteError("Camera '{0}': ONVIF call failed while resolving the snapshot URI ({1}): {2}{3}",
                camera.Name, camera.OnvifUri, ex.Message, ex.InnerException != null ? " - " + ex.InnerException.Message : "");
            throw;
        }

        try
        {
            var http = _snapshotHttpClients.GetOrAdd(camera.Name, _ => new HttpClient());

            // Some cameras' embedded HTTP server never sends the normal 401 challenge on an
            // unauthenticated request - it just drops the connection with zero bytes back regardless
            // of auth scheme or HTTP version (confirmed with curl against real hardware). Sending Basic
            // auth on the very first request covers that case; a camera that does the normal handshake
            // accepts a preemptive Basic header just as well.
            var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{camera.Username}:{camera.Password}"));
            using var request = new HttpRequestMessage(HttpMethod.Get, snapshotUri)
            {
                Version = HttpVersion.Version10,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                Headers = { Authorization = new AuthenticationHeaderValue("Basic", basicAuth) }
            };
            using var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            PackageHost.WriteError("Camera '{0}': downloading the ONVIF snapshot failed ({1}): {2}{3} - if this camera's ONVIF snapshot service doesn't work, set the \"RtspUrl\" setting to capture via RTSP/ffmpeg instead.",
                camera.Name, snapshotUri, ex.Message, ex.InnerException != null ? " - " + ex.InnerException.Message : "");
            throw;
        }
    }

    private static async Task<byte[]> CaptureRtspSnapshotAsync(CameraSetting camera)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        // ArgumentList (not a single Arguments string) so the RTSP URL's own special characters
        // (a password containing '&' or similar) can't be misparsed as extra ffmpeg flags.
        foreach (var arg in new[] { "-y", "-rtsp_transport", "tcp", "-i", camera.RtspUrl!, "-vframes", "1", "-q:v", "3", "-f", "mjpeg", "pipe:1" })
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start ffmpeg - is it installed and on the Edge's PATH?");

        using var stdout = new MemoryStream();
        // Both drained concurrently with the wait below, not after it - ffmpeg can otherwise deadlock
        // writing to a pipe nobody is reading yet if either buffer fills before the process exits.
        var copyStdoutTask = process.StandardOutput.BaseStream.CopyToAsync(stdout);
        var readStderrTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort - it may have exited on its own between the timeout firing and here.
            }
            throw new TimeoutException($"ffmpeg timed out capturing a frame from '{RedactCredentials(camera.RtspUrl)}'");
        }

        await copyStdoutTask;
        var stderr = RedactCredentials(await readStderrTask);
        if (process.ExitCode != 0 || stdout.Length == 0)
        {
            var detail = stderr.Length > 500 ? stderr[^500..] : stderr;
            throw new InvalidOperationException($"ffmpeg exited {process.ExitCode} with no image data - {detail}");
        }

        return stdout.ToArray();
    }

    // ffmpeg (and the ONVIF/HTTP error paths above) echo the URL they were given straight back into
    // their own error text - including any embedded "user:pass@" - so every message built from that
    // text needs this before it reaches a log line. Matches "scheme://user:pass@" generically rather
    // than assuming rtsp:// specifically, since the same helper covers an http(s) URL too.
    private static readonly Regex CredentialsInUrl = new(@"(?<scheme>[a-zA-Z][a-zA-Z0-9+.-]*://)[^/@\s]+@", RegexOptions.Compiled);

    private static string RedactCredentials(string text) => CredentialsInUrl.Replace(text, "${scheme}***@");
}
