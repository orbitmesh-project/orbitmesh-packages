# Changelog - OrbitMesh.NetworkTools

Format: [Keep a Changelog](https://keepachangelog.com/). Version matches `<Version>` in
`OrbitMesh.NetworkTools.csproj`, which is what gets published (see the repo root
[README.md](../README.md#publishing)).

## [1.1.3]

### Changed

- `OrbitMesh.Common` bumped to 1.2.5 - fixes `PackageHost` crashing with a secondary
  `FormatException` whenever a message handler threw an exception whose message contained literal
  `{`/`}` (e.g. an ffmpeg error), which replaced the real error in the log with an unrelated stack
  trace.
- `Microsoft.AspNetCore.SignalR.Common` 10.0.13 added as a direct reference, pinning the version of
  the SignalR assembly the package pulls in transitively.

## [1.1.2]

### Changed

- `OrbitMesh.Common` bumped to 1.2.2 - fixes a reconnect bug where `PackageHost` never sent the
  `IsReconnection` header, so the Server treated every reconnect (including an ordinary transient
  network blip) as brand new and purged this package's telemetry items, making values disappear for
  up to a full polling interval with nothing actually wrong.

## [1.1.1]

No functional change - version bump to exercise `publish-package.yml`.

## [1.1.0]

No functional change - version bump alongside the rest of this batch.
`PackageInfo.xml` already had the placeholder `Version` attribute the other packages in this batch
were missing, so this one was unaffected by that fix.

## [1.0.0] - 2026-08-13

Baseline - v1.
