using OrbitMesh.Package;

namespace OrbitMesh.Waze;

/// <summary>
/// Gets paths and estimated travel times from Waze's unofficial live-map routing service.
/// The original endpoint (www.waze.com/row-RoutingManager) is dead (verified: HTTP 410 Gone).
/// Fixed to use the still-live routing-livemap-{region}.waze.com endpoint (verified: HTTP 200
/// with a matching JSON shape).
/// </summary>
public sealed class WazePackage : IPackage
{
    private static readonly HttpClient HttpClient = new();
    private readonly WazeClient _client = new(HttpClient);
    private CancellationTokenSource? _cts;

    public static void Main(string[] args) => PackageHost.Start<WazePackage>(args);

    public void OnStart()
    {
        PackageHost.WriteInfo("Package starting - IsRunning: {0} - IsConnected: {1}", PackageHost.IsRunning, PackageHost.IsConnected);
        _cts = new CancellationTokenSource();
        _ = RunPollingLoopAsync(_cts.Token);
    }

    public void OnPreShutdown() => _cts?.Cancel();

    public void OnShutdown() => _cts?.Dispose();

    // Every configured MonitoredRoutes entry gets its own telemetry item (named "{FromPlace} to {ToPlace}"),
    // refreshed on a fixed interval - live traffic doesn't need to be polled every few seconds, and each
    // poll is a real outbound HTTP call to Waze's routing servers. Built on GetPlaceTraffic rather than
    // Trips so a round-trip (e.g. Home<->Daycare) only needs the two places defined once, not a pair of
    // near-duplicate Trip entries with swapped coordinates.
    private async Task RunPollingLoopAsync(CancellationToken cancellationToken)
    {
        while (PackageHost.IsRunning && !cancellationToken.IsCancellationRequested)
        {
            PackageHost.TryGetSettingValue<int>("RefreshIntervalSeconds", out var refreshIntervalSeconds, 300);
            foreach (var route in GetMonitoredRoutes())
            {
                try
                {
                    var routes = await GetPlaceTraffic(route.FromPlace, route.ToPlace, route.Region);
                    // Push just the primary route, not the whole alternatives list - a single Route gives
                    // a clean telemetry item Type (vs. the generic List<Route> type name) and the dashboard
                    // only ever wants "how long right now", not every alternative path.
                    if (routes.Count > 0)
                    {
                        PackageHost.PushTelemetryItem($"{route.FromPlace} to {route.ToPlace}", routes[0], lifetime: Math.Max(refreshIntervalSeconds * 2, 60));
                    }
                }
                catch (Exception ex)
                {
                    PackageHost.WriteError("Unable to refresh route '{0} -> {1}' : {2}", route.FromPlace, route.ToPlace, ex.Message);
                }
            }
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(refreshIntervalSeconds, 30)), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    [MessageHandler(Description = "Gets the available routes and travel times between two GPS points via Waze.")]
    public async Task<List<Route>> GetTraffic(double startLongitude, double startLatitude, double finishLongitude, double finishLatitude, WazeRegion region = WazeRegion.Eu)
    {
        var routes = await _client.GetRoutesAsync(startLongitude, startLatitude, finishLongitude, finishLatitude, region);
        foreach (var route in routes)
        {
            PackageHost.WriteInfo("{0} : {1} min (real-time)", route.Path, route.RealTime);
        }
        return routes;
    }

    [MessageHandler(Description = "Gets the current travel time for a named trip configured in the \"Trips\" setting.")]
    public async Task<List<Route>> GetTripTraffic(string tripName)
    {
        var trip = GetTrips().FirstOrDefault(t => t.Name.Equals(tripName, StringComparison.OrdinalIgnoreCase));
        if (trip == null)
        {
            PackageHost.WriteWarn("No trip named '{0}' in the \"Trips\" setting", tripName);
            return [];
        }
        return await GetTraffic(trip.StartLongitude, trip.StartLatitude, trip.FinishLongitude, trip.FinishLatitude, trip.Region);
    }

    [MessageHandler(Description = "Lists the trip names configured in the \"Trips\" setting.")]
    public List<string> GetTripNames() => GetTrips().Select(t => t.Name).ToList();

    [MessageHandler(Description = "Gets the current travel time between two named places configured in the \"Places\" setting (e.g. \"Home\" to \"Work\"), without needing a pre-defined trip for every combination.")]
    public async Task<List<Route>> GetPlaceTraffic(string fromPlace, string toPlace, WazeRegion region = WazeRegion.Eu)
    {
        var places = GetPlaces();
        var from = places.FirstOrDefault(p => p.Name.Equals(fromPlace, StringComparison.OrdinalIgnoreCase));
        var to = places.FirstOrDefault(p => p.Name.Equals(toPlace, StringComparison.OrdinalIgnoreCase));
        if (from == null || to == null)
        {
            PackageHost.WriteWarn("Unknown place(s) '{0}' -> '{1}': check the \"Places\" setting", fromPlace, toPlace);
            return [];
        }
        return await GetTraffic(from.Longitude, from.Latitude, to.Longitude, to.Latitude, region);
    }

    [MessageHandler(Description = "Lists the place names configured in the \"Places\" setting.")]
    public List<string> GetPlaceNames() => GetPlaces().Select(p => p.Name).ToList();

    private static List<TripSetting> GetTrips() => PackageHost.GetSettingAsJson<List<TripSetting>>("Trips") ?? [];

    private static List<PlaceSetting> GetPlaces() => PackageHost.GetSettingAsJson<List<PlaceSetting>>("Places") ?? [];

    private static List<MonitoredRouteSetting> GetMonitoredRoutes() => PackageHost.GetSettingAsJson<List<MonitoredRouteSetting>>("MonitoredRoutes") ?? [];
}
