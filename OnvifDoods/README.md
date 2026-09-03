# OrbitMesh.OnvifDoods

Watches ONVIF cameras for motion and runs a snapshot through a
[DOODS2](https://github.com/snowzach/doods2) object-detection server whenever motion fires,
publishing the result as a Telemetry Item.

- NuGet: `OrbitMesh.OnvifDoods`
- Depends on `OrbitMesh.Common` (see the main `orbitmesh` repo) and
  [SharpOnvifClient](https://github.com/jimm98y/SharpOnvif).
- Requires a running DOODS2 server reachable from the Edge this package runs on - this package only
  talks to DOODS2's REST API, it doesn't run detection itself.
- Requires `ffmpeg` installed and on the Edge's `PATH` **only** for a camera using `RtspUrl` (see
  below) - not needed at all if every camera uses the default ONVIF snapshot capture.

One instance handles every camera listed in the `Cameras` setting, rather than one instance per
camera - each OrbitMesh package instance is its own process, so N cameras as N instances pays that
process's baseline RAM N times over for what is, per camera, a handful of async calls. See
[Scheduled tasks](https://orbitmesh-project.github.io/orbitmesh/guide/architecture/scheduled-tasks)
for triggering something *else* off of a detection (e.g. turning on a light) - this package only
detects and reports, it doesn't act.

## How it decides when to run a detection

ONVIF motion events (an ONVIF PullPoint subscription, kept alive for as long as the package runs) are
the primary trigger. A camera's own `PollingIntervalSeconds`, if set above 0, additionally forces a
detection on a fixed timer - for something stationary a motion event would never fire for.
`MinSecondsBetweenDetections` is a per-camera cooldown: a motion event landing while a previous
detection is still within that window is ignored outright, so continuous motion (someone standing in
frame) doesn't spam DOODS2/Telemetry with one detection per event. The `DetectNow` message handler
always bypasses the cooldown.

Not every camera advertising ONVIF compliance has a working implementation of every service it
advertises - motion events or the snapshot service (or both) can turn out to be non-functional stubs
even though the SOAP calls resolve cleanly. `PollingIntervalSeconds` plus `EnableMotionEvents: false`
covers a camera whose motion events don't work (poll on a timer instead of subscribing to an event
that will never arrive - `EnableMotionEvents: false` also stops the pointless subscription/pull loop
and its "still listening" log heartbeat entirely); `RtspUrl` covers a camera whose ONVIF *snapshot*
doesn't work (grab the frame via RTSP/ffmpeg instead - see Settings below). The three are independent:
use any combination depending on what actually works on a given camera.

## Settings

| Name | Type | Required | Description |
|---|---|---|---|
| `Cameras` | JsonObject | yes | List of `{ Name, OnvifUri, Username, Password, ProfileToken, RtspUrl, EnableMotionEvents, MinSecondsBetweenDetections, PollingIntervalSeconds }`. `ProfileToken` empty uses the camera's first reported media profile. `RtspUrl` empty (the default) captures via the camera's ONVIF snapshot service; set it (e.g. `rtsp://user:pass@192.168.1.1:554/Streaming/Channels/101`) to capture via RTSP/ffmpeg instead, for a camera whose ONVIF snapshot service doesn't actually work. `EnableMotionEvents` (default `true`) set to `false` disables the ONVIF motion-event subscription for a camera confirmed to never fire one - pair with `PollingIntervalSeconds` as the sole trigger. |
| `DoodsUrl` | String | yes | Base URL of the DOODS2 server, no trailing path. |
| `DoodsDetectorName` | String | no (default `default`) | DOODS2 detector name, matching `doods.conf.yaml` on the DOODS2 server. |
| `Labels` | JsonObject | no (default `{"person": 60}`) | Object labels to detect and their minimum confidence (0-100) - passed straight through to DOODS2's own `detect` request field. |

## Message handlers

- `DetectNow(cameraName, includeImage = false)` - takes a snapshot from that camera right now, runs
  it through DOODS2, publishes and returns the result. Bypasses `MinSecondsBetweenDetections`.
  `includeImage: true` attaches the captured JPEG (base64, `ImageBase64`) to the result and its
  published Telemetry Item - off by default, see Telemetry below for why.
- `GetCameraNames()` - lists the camera names configured in `Cameras`.

## Telemetry

One Telemetry Item per camera, named after the camera, published every time a detection actually
runs: `{ CameraName, DetectedAtUtc, Detections: [{ Label, Confidence, Top, Left, Bottom, Right }], ImageBase64 }`.
`Confidence` is 0-100 (DOODS2's own scale); the box fields are 0-1 fractions of the image's
width/height. Deliberately just the raw DOODS2 output plus context - any "if person detected then X"
logic belongs in whichever other package reacts to it via `[TelemetryItemLink]`/
`RegisterTelemetryItemCallback`, not baked in here.

`ImageBase64` is null on every automatic detection (ONVIF motion event or `PollingIntervalSeconds`) -
only a `DetectNow` call with `includeImage: true` populates it. A base64 JPEG is a lot heavier than
everything else this package publishes (Telemetry Items push over SignalR to every current
subscriber on every update), so it's opt-in per call rather than something every continuous
detection carries by default.

See [CHANGELOG.md](CHANGELOG.md) for version history, and the repo root
[README.md](../README.md) for how to build and publish this package.
