# Changelog - OrbitMesh.DayInfo

Format: [Keep a Changelog](https://keepachangelog.com/). Version matches `<Version>` in
`OrbitMesh.DayInfo.csproj`, which is what gets published (see the repo root
[README.md](../README.md#publishing)).

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

- (.NET/Python) Switched name-day lookup from `fetes.txt` to `saints.json` (data.gouv.fr), fixing a
  May/June data swap in the source dataset.
