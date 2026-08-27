# OrbitMesh.DayInfo

Sunrise/sunset (NOAA solar calculator) and French name-day (almanac) info - no external API, no
API key needed.

- NuGet: `OrbitMesh.DayInfo`
- Depends on `OrbitMesh.Common` (see the main `orbitmesh` repo).
- Ported from [Constellation's `DayInfo` package](https://github.com/myconstellation/constellation-packages/tree/master/DayInfo) by Sébastien Warin (Apache License 2.0).

## Settings

| Name | Type | Required | Description |
|---|---|---|---|
| `TimeZone` | Int32 | yes | Your timezone offset. |
| `Latitude` | Double | yes | GPS latitude. |
| `Longitude` | Double | yes | GPS longitude. |

See [CHANGELOG.md](CHANGELOG.md) for version history, and the repo root
[README.md](../README.md) for how to build and publish this package.
