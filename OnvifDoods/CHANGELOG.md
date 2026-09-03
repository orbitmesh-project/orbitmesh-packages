# Changelog - OrbitMesh.OnvifDoods

Format: [Keep a Changelog](https://keepachangelog.com/). Version matches `<Version>` in
`OrbitMesh.OnvifDoods.csproj`, which is what gets published (see the repo root
[README.md](../README.md#publishing)).

## [1.3.1]

### Changed

- `OrbitMesh.Common` bumped to 1.2.2 - fixes a reconnect bug where `PackageHost` never sent the
  `IsReconnection` header, so the Server treated every reconnect (including an ordinary transient
  network blip) as brand new and purged this package's telemetry items, making values disappear for
  up to a full polling interval with nothing actually wrong.

## [1.3.0]

### Added

- `DetectNow` gained an `includeImage` parameter (default `false`) - attaches the captured JPEG,
  base64-encoded, as `DetectionResult.ImageBase64`. Opt-in and per-call only: the ONVIF-motion and
  `PollingIntervalSeconds` paths never populate it, since a base64 image is a lot heavier than
  everything else this package publishes and Telemetry Items push to every current subscriber on
  every update.

## [1.2.0]

### Added

- New per-camera `EnableMotionEvents` setting (default `true`). Set to `false` to skip the ONVIF
  PullPoint subscription/pull loop entirely for a camera confirmed to never fire a motion event -
  avoids polling such a camera forever and stops the "still listening" heartbeat log for it.
  `PollingIntervalSeconds` becomes the sole trigger when this is off.

### Changed

- Every successful detection now logs a line (snapshot size + capture method, and the DOODS2 result
  even when it found nothing) instead of only logging when an object was actually detected - there
  was previously no way to tell from the log whether RTSP/ONVIF capture was quietly succeeding with
  zero matches, or not running at all.

## [1.1.0]

### Added

- New per-camera `RtspUrl` setting: captures the detection frame via RTSP+ffmpeg instead of the
  camera's ONVIF snapshot service. Added after confirming against real hardware that a camera can
  advertise full ONVIF compliance (SOAP calls resolve cleanly) while its snapshot service is simply
  non-functional - RTSP is a much more universally reliable fallback than continuing to chase a
  broken ONVIF service. Requires `ffmpeg` on the Edge's `PATH`, but only when `RtspUrl` is actually
  set - a camera with a working ONVIF snapshot service needs no new dependency and is unaffected.
  ONVIF motion events remain the trigger either way; `RtspUrl` only changes how the frame itself is
  captured once triggered.

## [1.0.5]

### Fixed

- The HTTP/1.0 change in 1.0.4 didn't fix snapshot downloads because the real cause was different:
  confirmed with curl that this camera's snapshot endpoint drops the connection with zero bytes back
  ("Empty reply from server") on an unauthenticated request instead of sending a 401 challenge - it
  never negotiates auth, it expects credentials sent preemptively. `HttpClientHandler.Credentials`
  (like curl's default `-u`/`--digest`) only ever answers a challenge that was never coming. Now
  sends `Authorization: Basic` directly on the snapshot request instead of relying on that handshake.

## [1.0.4]

### Fixed

- Snapshot download failed against a real camera with "The response ended prematurely"
  (`HttpIOException`/`ResponseEnded`) even though the ONVIF SOAP calls to the same host:port worked
  fine - the camera's snapshot HTTP handler closes the connection to signal end-of-body instead of
  sending `Content-Length`/chunked framing, while still claiming HTTP/1.1, which modern `HttpClient`
  treats as a framing violation (curl/Postman tolerate it). The snapshot GET now explicitly requests
  HTTP/1.0, which expects a close-delimited body - exactly what these camera stacks actually send.

## [1.0.3]

### Added

- `DetectNow`/every triggered detection now logs which stage failed (resolving the ONVIF snapshot
  URI, downloading the snapshot, or the DOODS2 call itself) with the actual exception message and
  inner exception, instead of just letting a bare exception bubble up to the generic "Unable to
  dispatch the message" log line the message dispatcher already produces.

## [1.0.2]

### Fixed

- The ONVIF motion-event loop assumed `PullPointPullMessagesAsync`'s `timeoutInSeconds` makes the
  camera hold the HTTP response open (long-poll) for up to that long - a real camera whose ONVIF
  Events stack doesn't implement long-polling instead answers immediately with zero messages every
  time, which turned one iteration every ~30s into thousands of empty SOAP calls per minute
  (confirmed against real hardware: ~4000 pulls in 3 minutes instead of ~6). Added a 2-second floor
  on the loop's cadence, independent of how fast the camera actually responds.

## [1.0.1]

### Added

- Logs the raw ONVIF topic of any event that arrives but isn't recognized as motion (`OnvifEvents.IsMotionDetected`
  only matches `RuleEngine/CellMotionDetector/Motion`, `RuleEngine/MotionRegionDetector/Motion` and
  `VideoSource/MotionAlarm` - a cheap/OEM camera can emit motion under a different topic entirely).
  Also logs a heartbeat roughly every 5 minutes while a subscription is alive but has received
  nothing at all, to tell "still listening, camera genuinely isn't sending anything" apart from a
  silently hung loop.

## [1.0.0]

Baseline - v1.

### Added

- Watches one or more ONVIF cameras for motion (PullPoint subscription via `SharpOnvifClient`) and
  runs a snapshot through a DOODS2 object-detection server on each trigger, publishing the result as
  a Telemetry Item.
- Per-camera `MinSecondsBetweenDetections` cooldown and optional `PollingIntervalSeconds` fallback
  timer, alongside the primary ONVIF-event trigger.
- `DetectNow`/`GetCameraNames` message handlers.
