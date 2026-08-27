# OrbitMesh.ForecastIO

Global weather service for OrbitMesh. Rebuilt on Open-Meteo (free, keyless) after Dark Sky/forecast.io
was shut down by Apple in March 2023 - the name stuck for continuity, no relation to the original
service anymore.

- NuGet: `OrbitMesh.ForecastIO`
- Depends on `OrbitMesh.Common` (see the main `orbitmesh` repo).
- Ported from [Constellation's `ForecastIO` package](https://github.com/myconstellation/constellation-packages/tree/master/ForecastIO) by Sébastien Warin (Apache License 2.0).

## Settings

| Name | Type | Required | Description |
|---|---|---|---|
| `ForecastConfiguration` | JsonObject | yes | Refresh interval and monitored stations (name + lat/long). No API key needed. |

See [CHANGELOG.md](CHANGELOG.md) for version history, and the repo root
[README.md](../README.md) for how to build and publish this package.
