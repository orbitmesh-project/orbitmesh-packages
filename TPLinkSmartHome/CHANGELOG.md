# Changelog - OrbitMesh.TPLinkSmartHome

Format: [Keep a Changelog](https://keepachangelog.com/). Version matches `<Version>` in
`OrbitMesh.TPLinkSmartHome.csproj`, which is what gets published (see the repo root
[README.md](../README.md#publishing)).

## [1.1.4]

### Changed

- `OrbitMesh.Common` bumped to 1.2.2 - fixes a reconnect bug where `PackageHost` never sent the
  `IsReconnection` header, so the Server treated every reconnect (including an ordinary transient
  network blip) as brand new and purged this package's telemetry items, making values disappear for
  up to a full polling interval with nothing actually wrong.
- `KasaTapoClient` bumped to 1.3.1 - gates brightness control on the device's actual advertised
  capability instead of its device-type classification (split into `SupportsBrightnessControl`/
  `SupportsColorControl`/`SupportsColorTemperatureControl`), enabling it on more SMART-protocol
  devices (KS225, KS240, P135, S500D, S505D, S515D, HS220 rev. 3.26) that weren't previously
  recognized as dimmable.

## [1.1.3]

No functional change - version bump to exercise `publish-package.yml`.

## [1.1.2]

### Changed

- `KasaTapoClient` switched from a temporary local `ProjectReference` back to a normal NuGet
  `PackageReference` (`1.3.0`), now that a release with the dimmer-support fix
  (`SupportsLightControl` including `DeviceType.Dimmer`) has shipped and been verified to restore
  from nuget.org from a clean cache. No longer a blocker for CI publishing.

## [1.1.0]

### Fixed

- `PackageInfo.xml` was missing the placeholder `Version` attribute `Directory.Build.targets` needs
  to stamp the real `<Version>` into - `XmlPoke` can only overwrite an existing attribute, not
  create one, so it silently no-opped and the manifest kept reporting the hardcoded default
  (`"1.0.0"`) no matter what version was actually built/published.

## [1.0.0] - 2026-08-13

Baseline - v1.
