using OrbitMesh.Package;

namespace OrbitMesh.ForecastIO;

/// <summary>
/// Global weather service for OrbitMesh. The original package used Dark Sky (forecast.io),
/// which Apple shut down on 2023-03-31 - the API no longer exists at all, key or no key. Rebuilt
/// on Open-Meteo (https://open-meteo.com), a free, keyless, verified-live replacement, keeping the
/// original's per-station config and saga-based GetWeatherForecast callback.
/// </summary>
public sealed class ForecastIoPackage : IPackage
{
    private static readonly HttpClient HttpClient = new();
    private readonly OpenMeteoClient _client = new(HttpClient);
    private CancellationTokenSource? _cts;

    public static void Main(string[] args) => PackageHost.Start<ForecastIoPackage>(args);

    public void OnStart()
    {
        _cts = new CancellationTokenSource();
        _ = RunRefreshLoopAsync(_cts.Token);
    }

    public void OnPreShutdown() => _cts?.Cancel();

    public void OnShutdown() => _cts?.Dispose();

    private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        while (PackageHost.IsRunning && !cancellationToken.IsCancellationRequested)
        {
            var delaySeconds = 60;
            try
            {
                var settings = GetSettings();
                delaySeconds = Math.Max(settings.RefreshIntervalSeconds, 5);
                // Stations are independent GPS points hit against the same stateless API - refresh them
                // concurrently instead of one at a time.
                await Task.WhenAll(settings.Stations.Select(station => RefreshStationAsync(station, settings, cancellationToken)));
            }
            catch (Exception ex) when (ex is not TaskCanceledException)
            {
                // Otherwise a bad config (invalid JSON, unresolved {Variable}) fails this fire-and-forget loop silently.
                PackageHost.WriteError("Unable to refresh weather - check ForecastConfiguration : {0}", ex.Message);
            }
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    [MessageHandler(Description = "Gets the weather forecast for a given GPS location (saga only).")]
    public async Task<ForecastResult?> GetWeatherForecast(double longitude, double latitude)
    {
        if (!MessageContext.Current.IsSaga)
        {
            PackageHost.WriteWarn("This is not a saga !");
            return null;
        }
        return await _client.GetForecastAsync($"{latitude},{longitude}", latitude, longitude);
    }

    private static ForecastIoSettings GetSettings() =>
        PackageHost.GetSettingAsJson<ForecastIoSettings>("ForecastConfiguration", throwException: true)!;

    private async Task RefreshStationAsync(StationSetting station, ForecastIoSettings settings, CancellationToken cancellationToken)
    {
        PackageHost.WriteInfo("Getting forecast for {0}", station.Name);
        var result = await _client.GetForecastAsync(station.Name, station.Latitude, station.Longitude, cancellationToken);
        PackageHost.PushTelemetryItem(station.Name, result, lifetime: settings.RefreshIntervalSeconds * 2);
        if (result.LastError != null)
        {
            PackageHost.WriteError("Unable to get the weather for {0} : {1}", station.Name, result.LastError);
        }
        else
        {
            PackageHost.WriteInfo("Weather for {0} done. Report date @ {1}", station.Name, result.Current?.Time.ToLongTimeString() ?? "?");
        }
    }
}
