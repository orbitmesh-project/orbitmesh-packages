# OrbitMesh.Waze

Get paths and travel times from Waze's unofficial live-map routing service.

- NuGet: `OrbitMesh.Waze`
- Depends on `OrbitMesh.Common` (see the main `orbitmesh` repo).
- Ported from [Constellation's `Waze` package](https://github.com/myconstellation/constellation-packages/tree/master/Waze) by Hydro and Romain ODDONE (Apache License 2.0).

## Settings

| Name | Type | Required | Description |
|---|---|---|---|
| `Trips` | JsonObject | no | Named trips (`{Name, StartLatitude, StartLongitude, FinishLatitude, FinishLongitude, Region}`) - query with `GetTripTraffic(tripName)` instead of passing coordinates every time. |
| `Places` | JsonObject | no | Named GPS points (`{Name, Latitude, Longitude}`) - query any pair with `GetPlaceTraffic(fromPlace, toPlace)` instead of pre-defining every route combination as a Trip. |

See [CHANGELOG.md](CHANGELOG.md) for version history, and the repo root
[README.md](../README.md) for how to build and publish this package.
