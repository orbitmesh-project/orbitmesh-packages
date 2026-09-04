# Changelog - OrbitMesh.Ntfy

Format: [Keep a Changelog](https://keepachangelog.com/). Version matches `<Version>` in
`OrbitMesh.Ntfy.csproj`, which is what gets published (see the repo root
[README.md](../README.md#publishing)).

## [1.0.0]

Baseline - v1.

### Added

- `Notify` Shared message handler - any other package can call it directly without namespacing under
  `Ntfy/`, to send a push notification via ntfy (public instance or self-hosted) without needing its
  own ntfy integration.
