using System.Net.Http.Json;
using OrbitMesh.Utils;

namespace OrbitMesh.Waze;

/// <summary>
/// Client for Waze's unofficial live-map routing endpoint. The original package's URL
/// (www.waze.com/row-RoutingManager/routingRequest) now returns HTTP 410 Gone (verified live).
/// The routing servers moved to routing-livemap-{region}.waze.com, which is still live (verified
/// live, 200 OK with a matching JSON shape) - the same endpoint used by the WazeRouteCalculator
/// community project. This remains an unofficial/reverse-engineered API and can change again
/// without notice.
/// </summary>
public sealed class WazeClient(HttpClient httpClient)
{
    private const string CoordinatesFormat = "0.0000000";

    private static readonly Dictionary<WazeRegion, string> RoutingServers = new()
    {
        [WazeRegion.Us] = "https://routing-livemap-am.waze.com/RoutingManager/routingRequest",
        [WazeRegion.Eu] = "https://routing-livemap-row.waze.com/RoutingManager/routingRequest",
        [WazeRegion.Il] = "https://routing-livemap-il.waze.com/RoutingManager/routingRequest",
        [WazeRegion.Au] = "https://routing-livemap-row.waze.com/RoutingManager/routingRequest"
    };

    public async Task<List<Route>> GetRoutesAsync(double startLongitude, double startLatitude, double finishLongitude, double finishLatitude, WazeRegion region = WazeRegion.Eu, CancellationToken cancellationToken = default)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var from = $"x%3A{startLongitude.ToString(CoordinatesFormat, culture)}+y%3A{startLatitude.ToString(CoordinatesFormat, culture)}";
        var to = $"x%3A{finishLongitude.ToString(CoordinatesFormat, culture)}+y%3A{finishLatitude.ToString(CoordinatesFormat, culture)}";
        var url = $"{RoutingServers[region]}?from={from}&to={to}&at=0&returnJSON=true&timeout=60000&nPaths=3&options=AVOID_TRAILS%3At";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0");
        request.Headers.Referrer = new Uri("https://www.waze.com/");

        using var httpResponse = await httpClient.SendAsync(request, cancellationToken);
        httpResponse.EnsureSuccessStatusCode();

        var response = await httpResponse.Content.ReadFromJsonAsync<RoutingRequestResponse>(ObjectConverter.DefaultOptions, cancellationToken)
            ?? throw new InvalidOperationException("Empty response from Waze");

        var routes = new List<Route>();
        foreach (var alternative in response.Alternatives)
        {
            var sumTime = alternative.Response.Results.Sum(r => r.CrossTimeWithoutRealTime);
            var sumRealTime = alternative.Response.Results.Sum(r => r.CrossTime);
            routes.Add(new Route
            {
                Path = alternative.Response.RouteName ?? string.Empty,
                Time = sumTime / 60,
                RealTime = sumRealTime / 60,
                RouteType = alternative.Response.RouteType.FirstOrDefault()
            });
        }
        return routes;
    }
}
