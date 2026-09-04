# Changelog - OrbitMesh.Ntfy

Format: [Keep a Changelog](https://keepachangelog.com/). Version matches `<Version>` in
`OrbitMesh.Ntfy.csproj`, which is what gets published (see the repo root
[README.md](../README.md#publishing)).

## [1.0.1]

### Fixed

- `Notify` with `priority`/`tags` set always failed with a 400 from ntfy ("request body must be
  valid JSON" - a misleading message for what's actually a schema validation failure) - confirmed
  empirically against a real ntfy.sh request that the JSON publish endpoint requires `priority` as a
  number (1-5) and `tags` as a JSON array, unlike the header-based publish variant (which accepts a
  priority name like `"high"` and a comma-separated tags string - what this package was sending
  as-is). `Notify`'s own parameters are unchanged (still friendly strings, e.g. `priority: "high"`,
  `tags: "warning,skull"`) - the conversion now happens internally before the JSON request is built.

## [1.0.0]

Baseline - v1.

### Added

- `Notify` Shared message handler - any other package can call it directly without namespacing under
  `Ntfy/`, to send a push notification via ntfy (public instance or self-hosted) without needing its
  own ntfy integration.
