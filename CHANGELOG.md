# Changelog - orbitmesh-packages

Each package now keeps its own changelog, matching its own `<Version>` - see
[DayInfo](DayInfo/CHANGELOG.md), [ForecastIO](ForecastIO/CHANGELOG.md),
[NetworkTools](NetworkTools/CHANGELOG.md), [OpenWeather](OpenWeather/CHANGELOG.md),
[SonyBravia](SonyBravia/CHANGELOG.md), [Spotify](Spotify/CHANGELOG.md),
[TPLinkSmartHome](TPLinkSmartHome/CHANGELOG.md), [Waze](Waze/CHANGELOG.md).

## cicd/build-packages.ps1

Not tied to any one package's version - shared build tooling.

- 2026-08-21: `New-OrbitMeshNupkg` generated a nuspec with no `<readme>` element and never bundled
  the package's own `README.md` - `dotnet pack` even warned about it ("missing a readme"), and no
  feed (nuget.org, Pépite) had anything to show on the package page. Now adds `<readme>README.md</readme>`
  plus a matching `<file>` entry when a `README.md` exists next to the project's `.csproj` (every
  package here has one) - packing without one still works, just without the readme.

## [Baseline] - 2026-08-13

v1. Everything up to this point (DayInfo, ForecastIO, NetworkTools, OpenWeather, SonyBravia,
Spotify, TPLinkSmartHome, Waze, and the Python port of DayInfo) is considered the starting line,
not individually logged.
