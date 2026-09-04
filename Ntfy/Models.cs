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

    // The JSON publish endpoint wants an actual number (1-5), unlike the header-based publish variant
    // which also accepts a name ("high" etc.) - confirmed empirically against a real ntfy.sh request:
    // a string here fails with a misleading "request body must be valid JSON" 400, not a clear
    // validation error. NtfyClient.PublishAsync accepts the friendly string form and converts.
    [JsonPropertyName("priority")]
    public int? Priority { get; set; }

    // The JSON publish endpoint wants a JSON array, unlike the header-based publish variant's
    // comma-separated string - same "request body must be valid JSON" 400 otherwise, confirmed
    // empirically. NtfyClient.PublishAsync accepts the friendly comma-separated form and splits it.
    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }

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
            Priority = ParsePriority(priority),
            Tags = string.IsNullOrWhiteSpace(tags) ? null : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
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

    // Accepts either an already-numeric string ("4") or one of ntfy's own priority names, matching
    // what the header-based publish variant accepts - callers of the Notify message handler (a human
    // typing into the Console, or another package) shouldn't need to know the JSON endpoint's stricter
    // numeric-only requirement.
    private static int? ParsePriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            return null;
        }
        if (int.TryParse(priority, out var number))
        {
            return number;
        }
        return priority.Trim().ToLowerInvariant() switch
        {
            "min" => 1,
            "low" => 2,
            "default" => 3,
            "high" => 4,
            "max" or "urgent" => 5,
            _ => null
        };
    }
}
