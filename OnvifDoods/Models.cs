using System.Net.Http.Json;
using System.Text.Json.Serialization;
using OrbitMesh.Package;

namespace OrbitMesh.OnvifDoods;

/// <summary>One entry of the "Cameras" setting - everything needed to reach one ONVIF camera and
/// decide how eagerly to re-run detection on it.</summary>
public sealed class CameraSetting
{
    public required string Name { get; set; }

    /// <summary>The ONVIF device service URL, e.g. "http://192.168.1.1:8080/onvif/device_service" -
    /// found in the camera's own ONVIF settings page, not discovered automatically (WS-Discovery needs
    /// the package to sit on the same broadcast domain as the camera, which isn't guaranteed).</summary>
    public required string OnvifUri { get; set; }

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    /// <summary>Media profile to snapshot from. Empty (the default) uses the camera's first reported
    /// profile - only needed for a camera exposing more than one (e.g. a separate low-res substream).
    /// Unused when RtspUrl is set.</summary>
    public string ProfileToken { get; set; } = "";

    /// <summary>Empty (the default): grab the detection frame via the camera's own ONVIF snapshot
    /// service. Set to an RTSP URL (e.g. "rtsp://user:pass@192.168.1.1:554/Streaming/Channels/101")
    /// to grab it via RTSP+ffmpeg instead, for a camera whose ONVIF snapshot service doesn't actually
    /// work - confirmed against real hardware where GetSnapshotUri resolves cleanly but the URL it
    /// returns refuses every request, independent of auth strategy or HTTP version. ONVIF motion
    /// events (the trigger, separate from this) are unaffected either way - only the frame-grab
    /// mechanism changes. Requires ffmpeg installed and on the Edge's PATH.</summary>
    public string RtspUrl { get; set; } = "";

    /// <summary>True (the default): keep an ONVIF PullPoint subscription open for this camera's
    /// motion events. Set to false for a camera confirmed to never fire one (some cameras accept the
    /// subscription but never actually push anything through it - not hypothetical, seen against real
    /// hardware, independently confirmed with ONVIF Device Manager) - avoids polling that camera
    /// forever for an event it will never send, and stops filling the log with "still listening"
    /// heartbeats. PollingIntervalSeconds becomes the only trigger when this is false.</summary>
    public bool EnableMotionEvents { get; set; } = true;

    /// <summary>A motion event landing while a previous detection is still within this window is
    /// ignored outright (no new snapshot, no new DOODS2 call, no new Telemetry) - without it, someone
    /// standing in frame would re-trigger a full detection every time the camera re-fires its motion
    /// event, which most cameras do repeatedly for as long as motion continues. A manual DetectNow call
    /// always bypasses this.</summary>
    public int MinSecondsBetweenDetections { get; set; } = 30;

    /// <summary>0 (the default): detection only runs off the camera's own ONVIF motion events. Above
    /// 0: also force a detection on this fixed interval regardless of motion - e.g. to notice something
    /// stationary a PIR-style motion event would never fire for. Independent of, and in addition to,
    /// event-driven detection - not a replacement for it.</summary>
    public int PollingIntervalSeconds { get; set; }
}

/// <summary>Published as a Telemetry Item (named after the camera) every time a detection actually
/// runs - on a real ONVIF motion event past cooldown, a polling tick, or a manual DetectNow call.
/// Deliberately just the raw DOODS2 output plus context: this package reports what it saw, any
/// "if person detected then X" logic belongs in whichever other package reacts to it via
/// [TelemetryItemLink]/RegisterTelemetryItemCallback, not baked in here.</summary>
[TelemetryItem]
public sealed class DetectionResult
{
    public string CameraName { get; set; } = "";

    public DateTime DetectedAtUtc { get; set; }

    public List<Detection> Detections { get; set; } = [];

    /// <summary>Null unless explicitly requested via DetectNow's includeImage - the captured JPEG,
    /// base64-encoded. Never attached on the ONVIF-motion or PollingIntervalSeconds paths: those run
    /// continuously and push their result through Telemetry automatically, and an image is a lot
    /// heavier than everything else this package publishes - see the SDK docs on Telemetry sizing.
    /// Opt in per call instead of changing what every automatic detection publishes.</summary>
    public string? ImageBase64 { get; set; }
}

[TelemetryItem]
public sealed class Detection
{
    public string Label { get; set; } = "";

    /// <summary>0-100, DOODS2's own scale - not normalized to 0-1.</summary>
    public double Confidence { get; set; }

    /// <summary>Bounding box, each value 0-1 as a fraction of the image's width/height (DOODS2's own
    /// convention) - Top/Left is the box's top-left corner, Bottom/Right its bottom-right corner.</summary>
    public double Top { get; set; }

    public double Left { get; set; }

    public double Bottom { get; set; }

    public double Right { get; set; }
}

// DOODS2's own wire format (see https://github.com/snowzach/doods2) - kept file-private, package
// settings/Telemetry use the types above instead so a DOODS2 API change doesn't ripple out to
// whatever other package is consuming this one's Telemetry.
file sealed class DoodsDetectRequest
{
    [JsonPropertyName("detector_name")]
    public string DetectorName { get; set; } = "default";

    [JsonPropertyName("data")]
    public string Data { get; set; } = "";

    [JsonPropertyName("detect")]
    public Dictionary<string, double> Detect { get; set; } = [];
}

file sealed class DoodsDetectResponse
{
    [JsonPropertyName("detections")]
    public List<DoodsDetection>? Detections { get; set; }
}

file sealed class DoodsDetection
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("top")]
    public double Top { get; set; }

    [JsonPropertyName("left")]
    public double Left { get; set; }

    [JsonPropertyName("bottom")]
    public double Bottom { get; set; }

    [JsonPropertyName("right")]
    public double Right { get; set; }
}

/// <summary>Thin wrapper around DOODS2's single `/detect` REST endpoint - no SDK exists for it, it's
/// one JSON POST (see https://github.com/snowzach/doods2/blob/master/README.md).</summary>
public sealed class DoodsClient(HttpClient httpClient, string baseUrl)
{
    public async Task<List<Detection>> DetectAsync(byte[] jpegBytes, string detectorName, Dictionary<string, double> labels, CancellationToken cancellationToken)
    {
        var request = new DoodsDetectRequest
        {
            DetectorName = detectorName,
            Data = Convert.ToBase64String(jpegBytes),
            Detect = labels
        };
        using var response = await httpClient.PostAsJsonAsync($"{baseUrl.TrimEnd('/')}/detect", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DoodsDetectResponse>(cancellationToken: cancellationToken);
        return result?.Detections?.Select(d => new Detection
        {
            Label = d.Label,
            Confidence = d.Confidence,
            Top = d.Top,
            Left = d.Left,
            Bottom = d.Bottom,
            Right = d.Right
        }).ToList() ?? [];
    }
}
