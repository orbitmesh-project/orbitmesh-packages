using OrbitMesh.Package;

namespace OrbitMesh.Spotify;

/// <summary>
/// Polls Spotify's Web API for what's currently playing on the account, not a single device - unlike
/// a local Spotify Connect receiver (e.g. librespot), this reflects playback started from any device
/// (phone, soundbar, anything), which is the point: the user controls playback from wherever they want,
/// and this just reports what the account is doing right now. Authenticates via a refresh token obtained
/// once through a manual OAuth authorization (see LICENSE-NOTES-equivalent setup instructions), never
/// needing further user interaction as long as the Edge keeps running.
/// </summary>
public sealed class SpotifyPackage : IPackage
{
    private static readonly HttpClient HttpClient = new();
    private readonly SpotifyClient _client = new(HttpClient);
    private CancellationTokenSource? _cts;

    public static void Main(string[] args) => PackageHost.Start<SpotifyPackage>(args);

    public void OnStart()
    {
        PackageHost.WriteInfo("Package starting - IsRunning: {0} - IsConnected: {1}", PackageHost.IsRunning, PackageHost.IsConnected);
        _cts = new CancellationTokenSource();
        _ = RunPollingLoopAsync(_cts.Token);
    }

    public void OnPreShutdown() => _cts?.Cancel();

    public void OnShutdown() => _cts?.Dispose();

    private async Task RunPollingLoopAsync(CancellationToken cancellationToken)
    {
        while (PackageHost.IsRunning && !cancellationToken.IsCancellationRequested)
        {
            var settings = GetSettings();
            try
            {
                var playback = await _client.GetCurrentPlaybackAsync(settings, cancellationToken);
                PackageHost.PushTelemetryItem("NowPlaying", NowPlayingInfo.From(playback), lifetime: Math.Max(settings.PollIntervalSeconds * 3, 30));
            }
            catch (Exception ex)
            {
                PackageHost.WriteError("Unable to refresh Spotify playback state : {0}", ex.Message);
            }
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(settings.PollIntervalSeconds, 3)), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    [MessageHandler(Description = "Sets the volume (0-100) of whichever device is currently active for Spotify playback.")]
    public async Task SetVolume(int volumePercent)
    {
        var settings = GetSettings();
        try
        {
            await _client.SetVolumeAsync(settings, volumePercent);
        }
        catch (Exception ex)
        {
            PackageHost.WriteError("Unable to set Spotify volume : {0}", ex.Message);
        }
    }

    [MessageHandler(Description = "Resumes playback on whichever device is currently active.")]
    public Task Play() => RunPlayerCommandAsync(_client.PlayAsync, "resume");

    [MessageHandler(Description = "Pauses playback on whichever device is currently active.")]
    public Task Pause() => RunPlayerCommandAsync(_client.PauseAsync, "pause");

    [MessageHandler(Description = "Skips to the next track.")]
    public Task Next() => RunPlayerCommandAsync(_client.NextAsync, "skip to next track");

    [MessageHandler(Description = "Skips to the previous track.")]
    public Task Previous() => RunPlayerCommandAsync(_client.PreviousAsync, "skip to previous track");

    private async Task RunPlayerCommandAsync(Func<SpotifySettings, CancellationToken, Task> command, string description)
    {
        try
        {
            await command(GetSettings(), CancellationToken.None);
        }
        catch (Exception ex)
        {
            PackageHost.WriteError("Unable to {0} Spotify playback : {1}", description, ex.Message);
        }
    }

    private static SpotifySettings GetSettings() =>
        PackageHost.GetSettingAsJson<SpotifySettings>("SpotifyConfiguration", throwException: true)!;
}
