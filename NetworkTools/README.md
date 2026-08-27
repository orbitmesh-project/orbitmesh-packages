# OrbitMesh.NetworkTools

Network tools for OrbitMesh: ping, TCP port scanner/check, HTTP check, Wake on LAN, DNS lookup and
monitoring.

- NuGet: `OrbitMesh.NetworkTools`
- Depends on `OrbitMesh.Common` (see the main `orbitmesh` repo).
- Ported from [Constellation's `NetworkTools` package](https://github.com/myconstellation/constellation-packages/tree/master/NetworkTools) by Sébastien Warin and Hydro (Apache License 2.0).

## Settings

| Name | Type | Required | Description |
|---|---|---|---|
| `Monitoring` | JsonObject | no | The monitored resources - list of `{Name, Type: Ping\|Tcp\|Http, Hostname/Address, Interval, ...}`. |

See [CHANGELOG.md](CHANGELOG.md) for version history, and the repo root
[README.md](../README.md) for how to build and publish this package.
