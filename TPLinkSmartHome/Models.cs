using OrbitMesh.Package;
using KasaTapoClient;

namespace OrbitMesh.TPLinkSmartHome;

[TelemetryItem]
public class PlugInformations
{
    public string DeviceId { get; set; } = string.Empty;

    public string Alias { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string DeviceType { get; set; } = string.Empty;

    public string? FirmwareVersion { get; set; }

    public bool IsOn { get; set; }

    // Null for anything that isn't brightness-capable (device.Light.IsAvailable false) - a plain
    // relay switch/plug should never show a dimmer slider in a UI just because the field exists.
    public int? Brightness { get; set; }

    // SystemInfo is only null before the first successful update; CreateFrom is always called right
    // after Discover.ConnectAsync(..., updateState: true, ...), which guarantees it's populated.
    public static PlugInformations CreateFrom(KasaDevice device) => new()
    {
        DeviceId = device.SystemInfo!.DeviceId ?? string.Empty,
        Alias = device.Alias,
        Host = device.Host,
        Model = device.SystemInfo.Model ?? string.Empty,
        DeviceType = device.DeviceType.ToString(),
        FirmwareVersion = device.SystemInfo.SoftwareVersion,
        IsOn = device.IsOn ?? false,
        Brightness = device.Light.IsAvailable ? device.Light.State?.Brightness : null
    };
}

[TelemetryItem]
public sealed class PlugWithEnergyMeterInformations : PlugInformations
{
    public decimal PowerWatts { get; set; }

    public decimal VoltageVolts { get; set; }

    public decimal TodayKilowattHours { get; set; }

    public static new PlugWithEnergyMeterInformations CreateFrom(KasaDevice device)
    {
        var energy = device.EnergyUsage;
        var info = PlugInformations.CreateFrom(device);
        return new PlugWithEnergyMeterInformations
        {
            DeviceId = info.DeviceId,
            Alias = info.Alias,
            Host = info.Host,
            Model = info.Model,
            DeviceType = info.DeviceType,
            FirmwareVersion = info.FirmwareVersion,
            IsOn = info.IsOn,
            Brightness = info.Brightness,
            PowerWatts = energy != null ? Convert.ToDecimal(energy.CurrentPowerWatts) : 0,
            VoltageVolts = energy != null ? Convert.ToDecimal(energy.VoltageVolts) : 0,
            // KasaTapoClient's SMART-protocol parser puts today's consumption in TotalKilowattHours and
            // always leaves TodayKilowattHours null (confirmed empirically against a real KP125M) - read
            // whichever one is actually populated rather than trusting the property name.
            TodayKilowattHours = energy != null ? Convert.ToDecimal(energy.TodayKilowattHours ?? energy.TotalKilowattHours) : 0
        };
    }
}

/// <summary>One outlet of a multi-outlet device (e.g. the KP400 power strip) - each outlet is its own
/// on/off relay and is reported and controlled independently of its parent device.</summary>
[TelemetryItem]
public sealed class ChildOutletInformations
{
    public string ParentDeviceId { get; set; } = string.Empty;

    public string ChildId { get; set; } = string.Empty;

    public string Alias { get; set; } = string.Empty;

    public bool IsOn { get; set; }
}
