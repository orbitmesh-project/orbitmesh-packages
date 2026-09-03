# Changelog - OrbitMesh.Spotify

Format: [Keep a Changelog](https://keepachangelog.com/). Version matches `<Version>` in
`OrbitMesh.Spotify.csproj`, which is what gets published (see the repo root
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

### Fixed

- `PackageInfo.xml` was missing the placeholder `Version` attribute `Directory.Build.targets` needs
  to stamp the real `<Version>` into - `XmlPoke` can only overwrite an existing attribute, not
  create one, so it silently no-opped and the manifest kept reporting the hardcoded default
  (`"1.0.0"`) no matter what version was actually built/published.

## [1.0.0] - 2026-08-13

Baseline - v1.
