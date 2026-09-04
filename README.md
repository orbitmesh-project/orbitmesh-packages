# orbitmesh-packages

Official OrbitMesh packages: [DayInfo](DayInfo/README.md), [ForecastIO](ForecastIO/README.md),
[NetworkTools](NetworkTools/README.md), [Ntfy](Ntfy/README.md), [OnvifDoods](OnvifDoods/README.md),
[OpenWeather](OpenWeather/README.md), [SonyBravia](SonyBravia/README.md), [Spotify](Spotify/README.md),
[TPLinkSmartHome](TPLinkSmartHome/README.md), [Waze](Waze/README.md). Each is a consumer of the
`OrbitMesh.Common` SDK (from the separate `orbitmesh` repo) - kept in its own repo since these
version and publish independently of the platform itself. Each package has its own README and
CHANGELOG; this file only covers what's shared across all of them.

## Build

```powershell
Set-Location .\cicd
.\build-packages.ps1
# or just some of them:
.\build-packages.ps1 -Only TPLinkSmartHome,Spotify
```

Produces, per package, a `<Package>.zip` (drop into the Server's `packagesRootDirectory`) and a
`OrbitMesh.<Package>.<version>.nupkg` (for the [Pépite](https://github.com/forgelab-me/pepite) NuGet
V3 feed) under `cicd/build/`. `nuget.config` only lists `nuget.org`, needed to restore
`OrbitMesh.Common`.

## Publishing

`.github/workflows/publish-package.yml` builds and pushes one package's `.nupkg` to the official
feed (`https://nuget.orbitmesh.org`) whenever a tag matching `<FolderName>-v<version>` is pushed,
e.g. `DayInfo-v1.2.0` - the tag's `<FolderName>` must match a folder here exactly (case-sensitive),
and `<version>` must match that project's own `<Version>` in its `.csproj`. Authentication is
[Trusted Publishing](https://github.com/forgelab-me/pepite/blob/main/docs/trusted-publishing.md) -
no stored NuGet API key, a short-lived push key is minted per run from this repo's own GitHub OIDC
identity. Push tags one at a time (GitHub Actions silently drops `on.push.tags` triggers when
multiple tags land in the same `git push` - see the main `orbitmesh` repo's release workflows for
the same gotcha).

## Settings.json

Each package folder's `settings.json` is a local-dev convenience (picked up by the Edge when
running a package standalone, outside a Server-managed deploy) - never published, values there are
just realistic-looking examples/placeholders, not real credentials.
