using System.Text.Json.Serialization;
using OrbitMesh.Package;

namespace OrbitMesh.OpenWeather;

/// <summary>Supported "lang" values for OpenWeatherMap - see https://openweathermap.org/current#multi</summary>
public enum Language
{
    af, al, ar, az, bg, ca, cz, da, de, el, en, eu, fa, fi, fr, gl, he, hi, hr, hu, id, it, ja, kr, la, lt, mk,
    no, nl, pl, pt, pt_br, ro, ru, sv, se, sk, sl, sp, es, sr, th, tr, ua, uk, vi, zh_cn, zh_tw, zu
}

public sealed class Coord
{
    [JsonPropertyName("lon")]
    public double Longitude { get; init; }

    [JsonPropertyName("lat")]
    public double Latitude { get; init; }
}

public sealed class WeatherCondition
{
    public int Id { get; init; }

    public string Main { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Icon { get; init; } = string.Empty;
}

public sealed class Clouds
{
    public double All { get; init; }
}

public sealed class Wind
{
    public enum DirectionEnum
    {
        North, NorthNorthEast, NorthEast, EastNorthEast, East, EastSouthEast, SouthEast, SouthSouthEast,
        South, SouthSouthWest, SouthWest, WestSouthWest, West, WestNorthWest, NorthWest, NorthNorthWest, Unknown
    }

    [JsonPropertyName("speed")]
    public double SpeedMetersPerSecond { get; init; }

    [JsonIgnore]
    public double SpeedFeetPerSecond => SpeedMetersPerSecond * 3.28084;

    [JsonPropertyName("deg")]
    public double Degree { get; init; }

    public double Gust { get; init; }

    [JsonIgnore]
    public DirectionEnum Direction => Degree switch
    {
        >= 348.75 or <= 11.25 => DirectionEnum.North,
        > 11.25 and <= 33.75 => DirectionEnum.NorthNorthEast,
        > 33.75 and <= 56.25 => DirectionEnum.NorthEast,
        > 56.25 and <= 78.75 => DirectionEnum.EastNorthEast,
        > 78.75 and <= 101.25 => DirectionEnum.East,
        > 101.25 and <= 123.75 => DirectionEnum.EastSouthEast,
        > 123.75 and <= 146.25 => DirectionEnum.SouthEast,
        > 146.25 and <= 168.75 => DirectionEnum.SouthSouthEast,
        > 168.75 and <= 191.25 => DirectionEnum.South,
        > 191.25 and <= 213.75 => DirectionEnum.SouthSouthWest,
        > 213.75 and <= 236.25 => DirectionEnum.SouthWest,
        > 236.25 and <= 258.75 => DirectionEnum.WestSouthWest,
        > 258.75 and <= 281.25 => DirectionEnum.West,
        > 281.25 and <= 303.75 => DirectionEnum.WestNorthWest,
        > 303.75 and <= 326.25 => DirectionEnum.NorthWest,
        > 326.25 and <= 348.75 => DirectionEnum.NorthNorthWest,
        _ => DirectionEnum.Unknown
    };
}

public sealed class Precipitation
{
    [JsonPropertyName("1h")]
    public double H1 { get; init; }

    [JsonPropertyName("3h")]
    public double H3 { get; init; }
}

public sealed class TemperatureInfo
{
    [JsonPropertyName("temp")]
    public double KelvinCurrent { get; init; }

    [JsonPropertyName("temp_min")]
    public double KelvinMinimum { get; init; }

    [JsonPropertyName("temp_max")]
    public double KelvinMaximum { get; init; }

    public double Pressure { get; init; }

    public double Humidity { get; init; }

    [JsonPropertyName("sea_level")]
    public double SeaLevelAtm { get; init; }

    [JsonPropertyName("grnd_level")]
    public double GroundLevelAtm { get; init; }

    [JsonIgnore]
    public double CelsiusCurrent => Math.Round(KelvinCurrent - 273.15, 2);

    [JsonIgnore]
    public double CelsiusMinimum => Math.Round(KelvinMinimum - 273.15, 2);

    [JsonIgnore]
    public double CelsiusMaximum => Math.Round(KelvinMaximum - 273.15, 2);

    [JsonIgnore]
    public double FahrenheitCurrent => Math.Round(CelsiusCurrent * 9.0 / 5.0 + 32, 2);

    [JsonIgnore]
    public double FahrenheitMinimum => Math.Round(CelsiusMinimum * 9.0 / 5.0 + 32, 2);

    [JsonIgnore]
    public double FahrenheitMaximum => Math.Round(CelsiusMaximum * 9.0 / 5.0 + 32, 2);
}

/// <summary>One weather snapshot - shared shape between the "current weather" response and each "forecast" list entry.</summary>
public class WeatherSample
{
    [JsonPropertyName("weather")]
    public List<WeatherCondition> Conditions { get; init; } = [];

    [JsonPropertyName("main")]
    public TemperatureInfo MainInfo { get; init; } = new();

    public Wind Wind { get; init; } = new();

    public Clouds Clouds { get; init; } = new();

    public Precipitation? Rain { get; init; }

    public Precipitation? Snow { get; init; }

    [JsonPropertyName("dt")]
    [JsonConverter(typeof(UnixDateTimeConverter))]
    public DateTime Dt { get; init; }
}

/// <summary>The top-level "current weather" (/data/2.5/weather) response.</summary>
public class CurrentWeatherSample : WeatherSample
{
    [JsonPropertyName("base")]
    public string Base { get; init; } = string.Empty;

    public double Visibility { get; init; }
}

[TelemetryItem]
public sealed class WeatherInfo
{
    public int Cod { get; init; }

    public int CityId { get; init; }

    public string? Name { get; init; }

    public string? Country { get; init; }

    public DateTime Sunrise { get; init; }

    public DateTime Sunset { get; init; }

    public int Timezone { get; init; }

    public Coord? Coord { get; init; }

    public bool ValidRequestWeather { get; init; }

    public bool ValidRequestForecast { get; init; }

    public CurrentWeatherSample? Current { get; init; }

    public List<WeatherSample> Forecast { get; init; } = [];

    public string? LastError { get; init; }
}

public sealed class UnixDateTimeConverter : JsonConverter<DateTime>
{
    public static DateTime FromUnixSeconds(long seconds) => DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().DateTime;

    public override DateTime Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options) =>
        FromUnixSeconds(reader.GetInt64());

    public override void Write(System.Text.Json.Utf8JsonWriter writer, DateTime value, System.Text.Json.JsonSerializerOptions options) =>
        writer.WriteNumberValue(((DateTimeOffset)value).ToUnixTimeSeconds());
}
