# OrbitMesh.SonyBravia

Control your Sony Bravia devices from OrbitMesh (IRCC-IP + Pre-Shared Key, verified still supported
on Android TV).

- NuGet: `OrbitMesh.SonyBravia`
- Depends on `OrbitMesh.Common` (see the main `orbitmesh` repo) and `BraviaIRCCControl`.
- Ported from [Constellation's `SonyBravia` package](https://github.com/myconstellation/constellation-packages/tree/master/SonyBravia) by Romain ODDONE (Apache License 2.0).
- Requires enabling IP Control on the TV (Settings > Network > Home Network Setup > IP Control) and
  setting a Pre-Shared Key there.

## Settings

| Name | Type | Required | Description |
|---|---|---|---|
| `Hostname` | String | yes | The Bravia TV's IP address or hostname. |
| `Port` | Int32 | no (default `80`) | |
| `PinCode` | Password | no (default `0000`) | The Pre-Shared Key configured on the TV. |

See [CHANGELOG.md](CHANGELOG.md) for version history, and the repo root
[README.md](../README.md) for how to build and publish this package.
