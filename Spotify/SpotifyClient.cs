using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace OrbitMesh.Spotify;

/// <summary>
/// Thin wrapper around Spotify's Web API (https://api.spotify.com) and its OAuth token endpoint.
/// Holds the access token in memory and transparently renews it from the long-lived refresh token
/// (obtained once via a manual browser authorization) whenever it's missing or about to expire -
/// callers never see a token, only the two operations they actually need.
/// </summary>
public sealed class SpotifyClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private string? _accessToken;
    private DateTime _accessTokenExpiresAtUtc = DateTime.MinValue;

    /// <summary>Gets the account's current playback state, or null if nothing is playing anywhere
    /// on the account (Spotify returns 204 No Content in that case - not an error).</summary>
    internal async Task<SpotifyPlayerResponse?> GetCurrentPlaybackAsync(SpotifySettings settings, CancellationToken cancellationToken = default)
    {
        var token = await EnsureAccessTokenAsync(settings, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/me/player");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }
        await EnsureSuccessOrThrowWithBodyAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SpotifyPlayerResponse>(JsonOptions, cancellationToken);
    }

    public async Task SetVolumeAsync(SpotifySettings settings, int volumePercent, CancellationToken cancellationToken = default)
    {
        var token = await EnsureAccessTokenAsync(settings, cancellationToken);
        var clamped = Math.Clamp(volumePercent, 0, 100);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"https://api.spotify.com/v1/me/player/volume?volume_percent={clamped}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        // Spotify returns 404 here when no device is currently active - not worth treating as an error,
        // there's simply nothing to set the volume of right now.
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            await EnsureSuccessOrThrowWithBodyAsync(response, cancellationToken);
        }
    }

    public Task PlayAsync(SpotifySettings settings, CancellationToken cancellationToken = default) =>
        SendPlayerCommandAsync(settings, HttpMethod.Put, "play", cancellationToken);

    public Task PauseAsync(SpotifySettings settings, CancellationToken cancellationToken = default) =>
        SendPlayerCommandAsync(settings, HttpMethod.Put, "pause", cancellationToken);

    public Task NextAsync(SpotifySettings settings, CancellationToken cancellationToken = default) =>
        SendPlayerCommandAsync(settings, HttpMethod.Post, "next", cancellationToken);

    public Task PreviousAsync(SpotifySettings settings, CancellationToken cancellationToken = default) =>
        SendPlayerCommandAsync(settings, HttpMethod.Post, "previous", cancellationToken);

    private async Task SendPlayerCommandAsync(SpotifySettings settings, HttpMethod method, string action, CancellationToken cancellationToken)
    {
        var token = await EnsureAccessTokenAsync(settings, cancellationToken);
        using var request = new HttpRequestMessage(method, $"https://api.spotify.com/v1/me/player/{action}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        // Same reasoning as SetVolumeAsync - a 404 just means no device is currently active.
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            await EnsureSuccessOrThrowWithBodyAsync(response, cancellationToken);
        }
    }

    private async Task<string> EnsureAccessTokenAsync(SpotifySettings settings, CancellationToken cancellationToken)
    {
        if (_accessToken != null && DateTime.UtcNow < _accessTokenExpiresAtUtc)
        {
            return _accessToken;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
        var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.ClientId}:{settings.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = settings.RefreshToken
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessOrThrowWithBodyAsync(response, cancellationToken);
        var token = await response.Content.ReadFromJsonAsync<SpotifyTokenResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Empty token response from Spotify");

        _accessToken = token.AccessToken;
        // Renew a bit early so a request landing right at the boundary never gets caught mid-expiry.
        _accessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(token.ExpiresIn - 30, 30));
        return _accessToken;
    }

    // HttpResponseMessage.EnsureSuccessStatusCode() discards the response body, so a Spotify error like
    // {"error":"invalid_grant","error_description":"..."} - the actual useful part - never reaches the
    // logs, just a bare "400 (Bad Request)". Read the body first so WriteError actually says something.
    private static async Task EnsureSuccessOrThrowWithBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"Spotify returned {(int)response.StatusCode} ({response.StatusCode}) for {response.RequestMessage?.RequestUri}: {body}");
    }
}
