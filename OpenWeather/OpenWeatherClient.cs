using System.Net.Http.Json;
using OrbitMesh.Utils;

namespace OrbitMesh.OpenWeather;

public sealed class StationSetting
{
    public string Name { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }
}

public sealed class OpenWeatherSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public Language Language { get; set; } = Language.en;

    public int RefreshIntervalSeconds { get; set; } = 900;

    public List<StationSetting> Stations { get; set; } = [];
}

/// <summary>
/// Thin client for the OpenWeatherMap "Current Weather" and "5 Day / 3 Hour Forecast" APIs
/// (still live and free as of 2026 - https://openweathermap.org/api). No "units" is requested,
/// matching the original package's behavior: OWM returns Kelvin, converted locally to C/F.
/// </summary>
public sealed class OpenWeatherClient(HttpClient httpClient)
{
    private const string BaseUri = "https://api.openweathermap.org/data/2.5";

    public async Task<WeatherInfo> QueryWeatherAsync(string apiKey, double latitude, double longitude, Language language, CancellationToken cancellationToken = default)
    {
        var query = $"?appid={apiKey}&lat={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lang={language}";

        // The current-weather and forecast endpoints are independent requests - fetch them
        // concurrently instead of waiting for one before starting the other.
        var currentRequest = FetchAsync<CurrentWeatherResponse>($"{BaseUri}/weather{query}", cancellationToken);
        var forecastRequest = FetchAsync<ForecastResponse>($"{BaseUri}/forecast{query}", cancellationToken);
        await Task.WhenAll(currentRequest, forecastRequest);
        var (current, currentError) = currentRequest.Result;
        var (forecast, forecastError) = forecastRequest.Result;
        var lastError = currentError ?? forecastError;

        var validCurrent = current is { Cod: 200 };
        var validForecast = forecast is { Cod: "200" };

        return new WeatherInfo
        {
            Cod = current?.Cod ?? 0,
            CityId = validCurrent ? current!.Id : forecast?.City?.Id ?? 0,
            Name = validCurrent ? current!.Name : forecast?.City?.Name,
            Country = validCurrent ? current!.Sys?.Country : forecast?.City?.Country,
            Sunrise = ToLocalDateTime(validCurrent ? current!.Sys?.Sunrise : forecast?.City?.Sunrise),
            Sunset = ToLocalDateTime(validCurrent ? current!.Sys?.Sunset : forecast?.City?.Sunset),
            Timezone = validCurrent ? current!.Timezone : forecast?.City?.Timezone ?? 0,
            Coord = validCurrent ? current!.Coord : forecast?.City?.Coord,
            ValidRequestWeather = validCurrent,
            ValidRequestForecast = validForecast,
            Current = validCurrent ? current : null,
            Forecast = validForecast ? forecast!.List : [],
            LastError = lastError
        };
    }

    private async Task<(T? Value, string? Error)> FetchAsync<T>(string url, CancellationToken cancellationToken)
    {
        try
        {
            return (await httpClient.GetFromJsonAsync<T>(url, ObjectConverter.DefaultOptions, cancellationToken), null);
        }
        catch (Exception ex)
        {
            return (default, ex.Message);
        }
    }

    private static DateTime ToLocalDateTime(long? unixSeconds) =>
        unixSeconds is null or 0 ? default : UnixDateTimeConverter.FromUnixSeconds(unixSeconds.Value);

    private sealed class SysInfo
    {
        public string? Country { get; init; }
        public long Sunrise { get; init; }
        public long Sunset { get; init; }
    }

    private sealed class CityInfo
    {
        public int Id { get; init; }
        public string? Name { get; init; }
        public string? Country { get; init; }
        public Coord? Coord { get; init; }
        public int Timezone { get; init; }
        public long Sunrise { get; init; }
        public long Sunset { get; init; }
    }

    private sealed class CurrentWeatherResponse : CurrentWeatherSample
    {
        public int Cod { get; init; }
        public int Id { get; init; }
        public string? Name { get; init; }
        public int Timezone { get; init; }
        public SysInfo? Sys { get; init; }
        public Coord? Coord { get; init; }
    }

    private sealed class ForecastResponse
    {
        public string Cod { get; init; } = string.Empty;
        public List<WeatherSample> List { get; init; } = [];
        public CityInfo? City { get; init; }
    }
}
