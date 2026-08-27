# OrbitMesh.Spotify

Now-playing and volume control for Spotify via the official Web API - reflects the account's active
playback regardless of which device is actually outputting audio (phone, soundbar, this Edge
node's own speakers, anything), using a one-time OAuth authorization.

- NuGet: `OrbitMesh.Spotify`
- Depends on `OrbitMesh.Common` (see the main `orbitmesh` repo).
- Requires a Spotify Web API app (from [developer.spotify.com](https://developer.spotify.com)) and
  a refresh token obtained via the one-time OAuth authorization.

## Settings

| Name | Type | Required | Description |
|---|---|---|---|
| `SpotifyConfiguration` | JsonObject | yes | `{ClientId, ClientSecret, RefreshToken, PollIntervalSeconds}`. |

See [CHANGELOG.md](CHANGELOG.md) for version history, and the repo root
[README.md](../README.md) for how to build and publish this package.
