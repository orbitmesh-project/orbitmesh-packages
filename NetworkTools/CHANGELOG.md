# Changelog - OrbitMesh.NetworkTools

Format: [Keep a Changelog](https://keepachangelog.com/). Version matches `<Version>` in
`OrbitMesh.NetworkTools.csproj`, which is what gets published (see the repo root
[README.md](../README.md#publishing)).

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
