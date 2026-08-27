# OrbitMesh.OpenWeather

Weather service for OrbitMesh, based on the OpenWeatherMap v2.5 API (free tier, verified live).

- NuGet: `OrbitMesh.OpenWeather`
- Depends on `OrbitMesh.Common` (see the main `orbitmesh` repo).
- Ported from [Constellation's `OpenWeather` package](https://github.com/myconstellation/constellation-packages/tree/master/OpenWeather) by Sébastien Warin and Hydro (Apache License 2.0).
- Requires an OpenWeatherMap API key (free tier) - see `ApiKey` below.

## Settings

| Name | Type | Required | Description |
|---|---|---|---|
| `OpenWeatherConfiguration` | JsonObject | yes | `{ApiKey, Language, RefreshIntervalSeconds, Stations: [{Name, Latitude, Longitude}]}`. |

See [CHANGELOG.md](CHANGELOG.md) for version history, and the repo root
[README.md](../README.md) for how to build and publish this package.
