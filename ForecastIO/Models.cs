using System.Text.Json.Serialization;
using OrbitMesh.Package;

namespace OrbitMesh.ForecastIO;

public sealed class CurrentConditions
{
    public DateTime Time { get; init; }

    [JsonPropertyName("temperature_2m")]
    public double TemperatureCelsius { get; init; }

    [JsonPropertyName("relative_humidity_2m")]
    public double RelativeHumidityPercent { get; init; }

    [JsonPropertyName("apparent_temperature")]
    public double ApparentTemperatureCelsius { get; init; }

    public double Precipitation { get; init; }

    [JsonPropertyName("weather_code")]
    public int WeatherCode { get; init; }

    [JsonIgnore]
    public string WeatherDescription => WmoWeatherCode.Describe(WeatherCode);

    [JsonPropertyName("wind_speed_10m")]
    public double WindSpeedKmh { get; init; }

    [JsonPropertyName("wind_direction_10m")]
    public double WindDirectionDegrees { get; init; }

    [JsonPropertyName("pressure_msl")]
    public double PressureMsl { get; init; }

    [JsonPropertyName("cloud_cover")]
    public double CloudCoverPercent { get; init; }
}

public sealed class DailyForecastDay
{
    public DateOnly Date { get; init; }

    public DateTime Sunrise { get; init; }

    public DateTime Sunset { get; init; }

    public double TemperatureMinCelsius { get; init; }

    public double TemperatureMaxCelsius { get; init; }

    public double PrecipitationSumMm { get; init; }

    public int WeatherCode { get; init; }

    [JsonIgnore]
    public string WeatherDescription => WmoWeatherCode.Describe(WeatherCode);
}

[TelemetryItem]
public sealed class ForecastResult
{
    public string StationName { get; init; } = string.Empty;

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public string? Timezone { get; init; }

    public CurrentConditions? Current { get; init; }

    public List<DailyForecastDay> Daily { get; init; } = [];

    public string? LastError { get; init; }
}

/// <summary>WMO weather interpretation codes used by Open-Meteo - https://open-meteo.com/en/docs (see "WMO Weather interpretation codes").</summary>
public static class WmoWeatherCode
{
    public static string Describe(int code) => code switch
    {
        0 => "Clear sky",
        1 => "Mainly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 or 48 => "Fog",
        51 or 53 or 55 => "Drizzle",
        56 or 57 => "Freezing drizzle",
        61 or 63 or 65 => "Rain",
        66 or 67 => "Freezing rain",
        71 or 73 or 75 => "Snow fall",
        77 => "Snow grains",
        80 or 81 or 82 => "Rain showers",
        85 or 86 => "Snow showers",
        95 => "Thunderstorm",
        96 or 99 => "Thunderstorm with hail",
        _ => "Unknown"
    };
}

public sealed class StationSetting
{
    public string Name { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }
}

public sealed class ForecastIoSettings
{
    public int RefreshIntervalSeconds { get; set; } = 1800;

    public List<StationSetting> Stations { get; set; } = [];
}
