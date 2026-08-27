using OrbitMesh.Package;

namespace OrbitMesh.Spotify;

public sealed class SpotifySettings
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public int PollIntervalSeconds { get; set; } = 5;
}

/// <summary>Snapshot of the account's current playback, regardless of which device is actually
/// outputting audio - built from Spotify's GET /me/player response, or all-false/null when nothing
/// is playing anywhere on the account (that endpoint returns 204 No Content in that case).</summary>
[TelemetryItem]
public sealed class NowPlayingInfo
{
    public bool IsPlaying { get; set; }

    public string? TrackName { get; set; }

    public string? ArtistName { get; set; }

    public string? AlbumName { get; set; }

    public string? AlbumArtUrl { get; set; }

    public long ProgressMs { get; set; }

    public long DurationMs { get; set; }

    public string? DeviceName { get; set; }

    public int VolumePercent { get; set; }

    internal static NowPlayingInfo From(SpotifyPlayerResponse? response)
    {
        if (response?.Item == null)
        {
            return new NowPlayingInfo { IsPlaying = false };
        }
        return new NowPlayingInfo
        {
            IsPlaying = response.IsPlaying,
            TrackName = response.Item.Name,
            ArtistName = string.Join(", ", response.Item.Artists.Select(a => a.Name)),
            AlbumName = response.Item.Album?.Name,
            AlbumArtUrl = response.Item.Album?.Images.FirstOrDefault()?.Url,
            ProgressMs = response.ProgressMs,
            DurationMs = response.Item.DurationMs,
            DeviceName = response.Device?.Name,
            VolumePercent = response.Device?.VolumePercent ?? 0
        };
    }
}

// Internal DTOs matching Spotify's GET /me/player response shape (snake_case on the wire, mapped via
// SpotifyClient's JsonNamingPolicy.SnakeCaseLower rather than per-property JsonPropertyName attributes).
internal sealed class SpotifyPlayerResponse
{
    public SpotifyDevice? Device { get; set; }

    public bool IsPlaying { get; set; }

    public long ProgressMs { get; set; }

    public SpotifyTrack? Item { get; set; }
}

internal sealed class SpotifyDevice
{
    public string? Name { get; set; }

    public int VolumePercent { get; set; }
}

internal sealed class SpotifyTrack
{
    public string Name { get; set; } = string.Empty;

    public long DurationMs { get; set; }

    public List<SpotifyArtist> Artists { get; set; } = [];

    public SpotifyAlbum? Album { get; set; }
}

internal sealed class SpotifyArtist
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class SpotifyAlbum
{
    public string Name { get; set; } = string.Empty;

    public List<SpotifyImage> Images { get; set; } = [];
}

internal sealed class SpotifyImage
{
    public string Url { get; set; } = string.Empty;
}

internal sealed class SpotifyTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public int ExpiresIn { get; set; }
}
