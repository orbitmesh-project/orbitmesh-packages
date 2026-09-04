using OrbitMesh.Package;

namespace OrbitMesh.Ntfy;

/// <summary>
/// Sends push notifications via <see href="https://ntfy.sh">ntfy</see> (public instance or
/// self-hosted). Exposes a single Shared message handler so any other package can send a
/// notification without needing to know this package's name (see MessageHandlerAttribute.Shared) -
/// e.g. a Scheduled Task firing "Ntfy/Notify" directly, or another package relaying an alert through
/// it. No background work, no Telemetry - this package is purely reactive to Notify calls.
/// </summary>
public sealed class NtfyPackage : IPackage
{
    private static readonly HttpClient HttpClient = new();

    public static void Main(string[] args) => PackageHost.Start<NtfyPackage>(args);

    public void OnStart() => PackageHost.WriteInfo("Package starting - IsRunning: {0} - IsConnected: {1}", PackageHost.IsRunning, PackageHost.IsConnected);

    public void OnPreShutdown() { }

    public void OnShutdown() { }

    [MessageHandler(Shared = true, Description = "Sends a push notification via ntfy. topic defaults to the \"DefaultTopic\" setting when omitted.")]
    public async Task Notify(string message, string? title = null, string? topic = null, string? priority = null, string? tags = null, string? click = null)
    {
        var resolvedTopic = string.IsNullOrEmpty(topic) ? PackageHost.GetSettingValue<string>("DefaultTopic") : topic;
        if (string.IsNullOrEmpty(resolvedTopic))
        {
            PackageHost.WriteWarn("Notify called with no topic and no \"DefaultTopic\" setting configured - nothing sent.");
            return;
        }

        var serverUrl = PackageHost.GetSettingValue<string>("ServerUrl") is { Length: > 0 } url ? url : "https://ntfy.sh";
        var username = PackageHost.GetSettingValue<string>("Username");
        var password = PackageHost.GetSettingValue<string>("Password");
        var accessToken = PackageHost.GetSettingValue<string>("AccessToken");

        var client = new NtfyClient(HttpClient, serverUrl, username, password, accessToken);
        try
        {
            await client.PublishAsync(resolvedTopic, message, title, priority, tags, click, CancellationToken.None);
            PackageHost.WriteInfo("Notify: sent to topic '{0}'{1}", resolvedTopic, title != null ? $" (title: '{title}')" : "");
        }
        catch (Exception ex)
        {
            PackageHost.WriteError("Notify: failed to publish to '{0}' topic '{1}': {2}{3}",
                serverUrl, resolvedTopic, ex.Message, ex.InnerException != null ? " - " + ex.InnerException.Message : "");
            throw;
        }
    }
}
