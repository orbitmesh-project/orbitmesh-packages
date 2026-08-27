using System.Net.Http.Json;
using System.Text.Json.Serialization;
using OrbitMesh.Utils;

namespace OrbitMesh.ForecastIO;

/// <summary>
/// Client for Open-Meteo (https://open-meteo.com), a free/keyless weather API used here as the
/// replacement for Dark Sky (forecast.io), which Apple shut down in March 2023. Verified live
/// against api.open-meteo.com/v1/forecast.
/// </summary>
public sealed class OpenMeteoClient(HttpClient httpClient)
{
    private const string BaseUri = "https://api.open-meteo.com/v1/forecast";
    private const string CurrentParams = "temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m,wind_direction_10m,pressure_msl,cloud_cover";
    private const string DailyParams = "weather_code,sunrise,sunset,temperature_2m_max,temperature_2m_min,precipitation_sum";

    public async Task<ForecastResult> GetForecastAsync(string stationName, double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var lat = latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lon = longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var url = $"{BaseUri}?latitude={lat}&longitude={lon}&current={CurrentParams}&daily={DailyParams}&timezone=auto&forecast_days=7";

        try
        {
            var raw = await httpClient.GetFromJsonAsync<RawResponse>(url, ObjectConverter.DefaultOptions, cancellationToken)
                ?? throw new InvalidOperationException("Empty response");

            var daily = new List<DailyForecastDay>();
            for (var i = 0; i < raw.Daily.Time.Count; i++)
            {
                daily.Add(new DailyForecastDay
                {
                    Date = DateOnly.Parse(raw.Daily.Time[i]),
                    Sunrise = DateTime.Parse(raw.Daily.Sunrise[i]),
                    Sunset = DateTime.Parse(raw.Daily.Sunset[i]),
                    TemperatureMinCelsius = raw.Daily.Temperature2mMin[i],
                    TemperatureMaxCelsius = raw.Daily.Temperature2mMax[i],
                    PrecipitationSumMm = raw.Daily.PrecipitationSum[i],
                    WeatherCode = raw.Daily.WeatherCode[i]
                });
            }

            return new ForecastResult
            {
                StationName = stationName,
                Latitude = raw.Latitude,
                Longitude = raw.Longitude,
                Timezone = raw.Timezone,
                // Current is already shaped exactly like the public CurrentConditions model (same
                // [JsonPropertyName] mappings), so it deserializes directly - no separate raw DTO needed.
                Current = raw.Current,
                Daily = daily
            };
        }
        catch (Exception ex)
        {
            return new ForecastResult { StationName = stationName, Latitude = latitude, Longitude = longitude, LastError = ex.Message };
        }
    }

    private sealed class RawResponse
    {
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public string Timezone { get; init; } = string.Empty;
        public CurrentConditions Current { get; init; } = new();
        public RawDaily Daily { get; init; } = new();
    }

    private sealed class RawDaily
    {
        public List<string> Time { get; init; } = [];
        public List<string> Sunrise { get; init; } = [];
        public List<string> Sunset { get; init; } = [];
        [JsonPropertyName("temperature_2m_max")] public List<double> Temperature2mMax { get; init; } = [];
        [JsonPropertyName("temperature_2m_min")] public List<double> Temperature2mMin { get; init; } = [];
        [JsonPropertyName("precipitation_sum")] public List<double> PrecipitationSum { get; init; } = [];
        [JsonPropertyName("weather_code")] public List<int> WeatherCode { get; init; } = [];
    }
}
