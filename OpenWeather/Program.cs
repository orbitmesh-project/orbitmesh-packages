using OrbitMesh.Package;

namespace OrbitMesh.OpenWeather;

/// <summary>
/// Weather service based on OpenWeatherMap (still live/free on the v2.5 endpoints as of 2026:
/// https://api.openweathermap.org/data/2.5/weather and /forecast). Modernized: JSON station
/// settings instead of a Windows ConfigurationSection, HttpClient + System.Text.Json instead of
/// WebClient + manual JToken parsing.
/// </summary>
public sealed class OpenWeatherPackage : IPackage
{
    private static readonly HttpClient HttpClient = new();
    private readonly OpenWeatherClient _client = new(HttpClient);
    private CancellationTokenSource? _cts;

    public static void Main(string[] args) => PackageHost.Start<OpenWeatherPackage>(args);

    public void OnStart()
    {
        _cts = new CancellationTokenSource();
        PackageHost.WriteInfo("Package starting - IsRunning: {0} - IsConnected: {1}", PackageHost.IsRunning, PackageHost.IsConnected);
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
                PackageHost.WriteError("Unable to refresh weather - check OpenWeatherConfiguration : {0}", ex.Message);
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

    [MessageHandler(Description = "Gets the current weather and forecast for a given GPS location (saga only).")]
    public async Task<WeatherInfo?> GetWeatherForecast(double longitude, double latitude)
    {
        if (!MessageContext.Current.IsSaga)
        {
            PackageHost.WriteWarn("This is not a saga !");
            return null;
        }
        var settings = GetSettings();
        return await _client.QueryWeatherAsync(settings.ApiKey, latitude, longitude, settings.Language);
    }

    private static OpenWeatherSettings GetSettings() =>
        PackageHost.GetSettingAsJson<OpenWeatherSettings>("OpenWeatherConfiguration", throwException: true)!;

    private async Task RefreshStationAsync(StationSetting station, OpenWeatherSettings settings, CancellationToken cancellationToken)
    {
        PackageHost.WriteInfo("Getting forecast for {0} ...", station.Name);
        try
        {
            var result = await _client.QueryWeatherAsync(settings.ApiKey, station.Latitude, station.Longitude, settings.Language, cancellationToken);
            PackageHost.PushTelemetryItem(station.Name, result, lifetime: settings.RefreshIntervalSeconds * 2);
            if (result.LastError != null)
            {
                PackageHost.WriteError("Unable to get the weather for {0} : {1}", station.Name, result.LastError);
            }
            else
            {
                PackageHost.WriteInfo("Weather for {0} updated.", station.Name);
            }
        }
        catch (Exception ex)
        {
            PackageHost.WriteError("Unable to get the weather for {0} : {1}", station.Name, ex);
        }
    }
}
