using OrbitMesh.Package;

namespace OrbitMesh.NetworkTools;

[TelemetryItem]
public sealed class MonitoringResult
{
    /// <summary>Response time in milliseconds (-1 if unreachable).</summary>
    public long ResponseTime { get; set; }

    public bool State { get; set; }
}

/// <summary>One entry of the "Monitoring" JSON setting.</summary>
public sealed class MonitoringResource
{
    public string Name { get; set; } = string.Empty;

    /// <summary>"Ping", "Tcp" or "Http" (case-insensitive).</summary>
    public string Type { get; set; } = string.Empty;

    public string? Hostname { get; set; }

    public int? Port { get; set; }

    public string? Address { get; set; }

    public string? Regex { get; set; }

    public int? Interval { get; set; }

    public int? Timeout { get; set; }
}
