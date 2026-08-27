using OrbitMesh.Package;

namespace OrbitMesh.Waze;

/// <summary>Waze routing server region - see https://routing-livemap-{region}.waze.com</summary>
public enum WazeRegion
{
    Us,
    Eu,
    Il,
    Au
}

[TelemetryItem]
public sealed class Route
{
    public string Path { get; init; } = string.Empty;

    /// <summary>Estimated time in minutes without live traffic.</summary>
    public int Time { get; init; }

    /// <summary>Estimated time in minutes with live traffic.</summary>
    public int RealTime { get; init; }

    public string? RouteType { get; init; }
}

/// <summary>A named, pre-configured trip - see the "Trips" setting in PackageInfo.xml.</summary>
public sealed class TripSetting
{
    public string Name { get; set; } = string.Empty;

    public double StartLongitude { get; set; }

    public double StartLatitude { get; set; }

    public double FinishLongitude { get; set; }

    public double FinishLatitude { get; set; }

    public WazeRegion Region { get; set; } = WazeRegion.Eu;
}

/// <summary>A named GPS point (Home, Work, Daycare...) - see the "Places" setting in PackageInfo.xml.</summary>
public sealed class PlaceSetting
{
    public string Name { get; set; } = string.Empty;

    public double Longitude { get; set; }

    public double Latitude { get; set; }
}

/// <summary>A route between two named Places to poll periodically - see the "MonitoredRoutes" setting.</summary>
public sealed class MonitoredRouteSetting
{
    public string FromPlace { get; set; } = string.Empty;

    public string ToPlace { get; set; } = string.Empty;

    public WazeRegion Region { get; set; } = WazeRegion.Eu;
}

internal sealed class RoutingRequestResponse
{
    public List<Alternative> Alternatives { get; set; } = [];
}

internal sealed class Alternative
{
    public RouteResponse Response { get; set; } = new();
}

internal sealed class RouteResponse
{
    public List<RouteResult> Results { get; set; } = [];
    public string? RouteName { get; set; }
    public List<string> RouteType { get; set; } = [];
}

internal sealed class RouteResult
{
    public int CrossTime { get; set; }
    public int CrossTimeWithoutRealTime { get; set; }
}
