# OrbitMesh.TPLinkSmartHome

Discover and control TP-Link Kasa/Tapo devices on the local network (legacy, KLAP and TPAP
protocols).

- NuGet: `OrbitMesh.TPLinkSmartHome`
- Depends on `OrbitMesh.Common` (see the main `orbitmesh` repo) and `KasaTapoClient`.
- Ported from [Constellation's `TPLinkSmartHome` package](https://github.com/myconstellation/constellation-packages/tree/master/TPLinkSmartHome) by Romain ODDONE (Apache License 2.0).
- Requires the TP-Link/Kasa account email+password (same credential as the Kasa/Tapo mobile app) -
  needed by KLAP/TPAP-authenticated devices (2023+ Kasa firmware, all Tapo devices).

## Settings

| Name | Type | Required | Description |
|---|---|---|---|
| `poolingInterval` | Int32 | no (default `10000`) | How often (ms) to refresh already-discovered devices. |
| `discoveryIntervalMs` | Int32 | no (default `60000`) | How often (ms) to re-broadcast a LAN discovery pass. Can be forced early via `ForceDiscovery`. |
| `Username` | String | yes | TP-Link/Kasa account email. |
| `Password` | Password | yes | TP-Link/Kasa account password. |

See [CHANGELOG.md](CHANGELOG.md) for version history, and the repo root
[README.md](../README.md) for how to build and publish this package.
