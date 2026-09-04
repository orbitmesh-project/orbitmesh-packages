# OrbitMesh.Ntfy

Sends push notifications via [ntfy](https://ntfy.sh) (the public instance, or a self-hosted one).

- NuGet: `OrbitMesh.Ntfy`
- Depends on `OrbitMesh.Common` (see the main `orbitmesh` repo).
- No account/app needed for the public instance and an unclaimed topic - just pick a topic name and
  subscribe to it in the ntfy app/web UI. Anyone who knows the topic name can publish/subscribe to it,
  so treat it like an unlisted URL for anything sensitive, or use a self-hosted instance with auth.

## Message handlers

- `Notify(message, title, topic, priority, tags, click)` - **Shared** (see
  [Messages](https://orbitmesh-project.github.io/orbitmesh/guide/sdk/messages)), so any other package
  can call it directly as `"Notify"` without namespacing under `Ntfy/`. Only `message` is required:
  - `title` - notification title.
  - `topic` - overrides the `DefaultTopic` setting for this one call.
  - `priority` - `"min"`/`"low"`/`"default"`/`"high"`/`"urgent"`, or `"1"`-`"5"`.
  - `tags` - comma-separated [emoji shortcodes](https://docs.ntfy.sh/publish/#tags-emojis) (e.g.
    `"warning,skull"`).
  - `click` - a URL to open when the notification is tapped.

## Settings

| Name | Type | Required | Description |
|---|---|---|---|
| `ServerUrl` | String | no (default `https://ntfy.sh`) | Base URL of the ntfy server, no trailing path. |
| `DefaultTopic` | String | yes | Topic to publish to when a `Notify` call doesn't specify one. |
| `Username` | String | no | For a protected topic/self-hosted instance using account auth (Basic). |
| `Password` | Password | no | Paired with `Username`. |
| `AccessToken` | Password | no | ntfy access token (Bearer auth) - an alternative to `Username`/`Password`, preferred when both are set. |

Leave `Username`/`Password`/`AccessToken` all empty to publish unauthenticated, e.g. to a topic on the
public `ntfy.sh` instance.

See [CHANGELOG.md](CHANGELOG.md) for version history, and the repo root
[README.md](../README.md) for how to build and publish this package.
