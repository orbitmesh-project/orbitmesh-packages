using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace OrbitMesh.Ntfy;

// ntfy's own JSON publish format (https://docs.ntfy.sh/publish/#publish-as-json) - POST this
// straight to the server's base URL, not to "{ServerUrl}/{topic}" (that's the plain-text/header
// variant, which requires RFC 2047-encoding any non-ASCII title - the JSON variant sidesteps that
// entirely by keeping title/message as ordinary UTF-8 JSON string values).
file sealed class NtfyPublishRequest
{
    [JsonPropertyName("topic")]
    public required string Topic { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    // Accepts either a number (1-5) or a name ("min"/"low"/"default"/"high"/"urgent") - passed
    // through as-is rather than parsed/validated here, ntfy already does that server-side.
    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    // Comma-separated (e.g. "warning,skull"), not a JSON array - ntfy's own convention.
    [JsonPropertyName("tags")]
    public string? Tags { get; set; }

    [JsonPropertyName("click")]
    public string? Click { get; set; }
}

/// <summary>Thin wrapper around ntfy's single JSON publish endpoint - no SDK exists for it, it's one
/// JSON POST (see https://docs.ntfy.sh/publish/).</summary>
public sealed class NtfyClient(HttpClient httpClient, string serverUrl, string? username, string? password, string? accessToken)
{
    public async Task PublishAsync(string topic, string message, string? title, string? priority, string? tags, string? click, CancellationToken cancellationToken)
    {
        var request = new NtfyPublishRequest
        {
            Topic = topic,
            Message = message,
            Title = title,
            Priority = priority,
            Tags = tags,
            Click = click
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, serverUrl.TrimEnd('/') + "/")
        {
            Content = JsonContent.Create(request)
        };

        // AccessToken takes priority when both are configured - it's ntfy's more modern mechanism
        // (a per-token scope/expiry, unlike a bare account password) and simpler to rotate/revoke.
        if (!string.IsNullOrEmpty(accessToken))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        else if (!string.IsNullOrEmpty(username))
        {
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        }

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
