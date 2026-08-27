using OrbitMesh.DayInfo.Utils;
using OrbitMesh.Package;

namespace OrbitMesh.DayInfo;

public static class Program
{
    private static void Main(string[] args) => PackageHost.Start<DayInfoPackage>(args);
}

/// <summary>
/// Sunrise/sunset (NOAA solar calculator, pure math - no external API) and French name-day lookup
/// (static almanac data - no external API). Nothing here can go stale via a third-party API change.
/// </summary>
public sealed class DayInfoPackage : IPackage
{
    private DateOnly _dateProcessed = DateOnly.MinValue;
    private CancellationTokenSource? _cts;

    public void OnStart()
    {
        _cts = new CancellationTokenSource();
        _ = RunDailyLoopAsync(_cts.Token);
    }

    public void OnPreShutdown() => _cts?.Cancel();

    public void OnShutdown() => _cts?.Dispose();

    private async Task RunDailyLoopAsync(CancellationToken cancellationToken)
    {
        while (PackageHost.IsRunning && !cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);
            if (today != _dateProcessed)
            {
                PackageHost.PushTelemetryItem("SunInfo", GetSunInfo(now,
                    PackageHost.GetSettingValue<int>("TimeZone"),
                    PackageHost.GetSettingValue<double>("Latitude"),
                    PackageHost.GetSettingValue<double>("Longitude")));
                PackageHost.PushTelemetryItem("NameDay", NameDayUtils.GetNameDay(), metadatas: new Dictionary<string, object> { ["Date"] = now });
                _dateProcessed = today;
                PackageHost.WriteInfo("TelemetryItems updated for today ({0})", _dateProcessed);
            }
            // Wake up shortly after next local midnight instead of polling every second for a value
            // that only changes once a day - cancellation still interrupts this delay immediately.
            var delay = today.ToDateTime(TimeOnly.MinValue).AddDays(1).AddSeconds(1) - now;
            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    [MessageHandler(Description = "Gets the name day (French almanac) for the given date.")]
    public string GetNameDay(DateOnly date) => NameDayUtils.GetNameDay(date.ToDateTime(TimeOnly.MinValue));

    [MessageHandler(Description = "Calculates the UTC sunrise/sunset for the given date and location.")]
    public SunInfo GetSunInfo(DateOnly date, int timezone, double latitude, double longitude) =>
        GetSunInfo(date.ToDateTime(TimeOnly.MinValue), timezone, latitude, longitude);

    private static SunInfo GetSunInfo(DateTime date, int timezone, double latitude, double longitude)
    {
        double jd = NAAUtils.CalcJD(date);
        double sunRise = NAAUtils.CalcSunRiseUTC(jd, latitude, longitude);
        double sunSet = NAAUtils.CalcSunSetUTC(jd, latitude, longitude);
        bool isDaylightSavingTime = TimeZoneInfo.Local.IsDaylightSavingTime(date);
        return new SunInfo
        {
            Date = DateTime.Now.Date,
            TimeZone = timezone,
            Longitude = longitude,
            Latitude = latitude,
            DayLightSavings = isDaylightSavingTime,
            Sunrise = NAAUtils.GetDateTime(sunRise, timezone, DateTime.Now, isDaylightSavingTime)!.Value.TimeOfDay,
            Sunset = NAAUtils.GetDateTime(sunSet, timezone, DateTime.Now, isDaylightSavingTime)!.Value.TimeOfDay
        };
    }
}

[TelemetryItem]
public sealed class SunInfo
{
    public DateTime Date { get; set; }
    public int TimeZone { get; set; }
    public double Longitude { get; set; }
    public double Latitude { get; set; }
    public bool DayLightSavings { get; set; }
    public TimeSpan Sunrise { get; set; }
    public TimeSpan Sunset { get; set; }
}
