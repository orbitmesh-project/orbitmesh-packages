using BraviaIRCCControl;
using OrbitMesh.Package;

namespace OrbitMesh.SonyBravia;

/// <summary>
/// Controls Sony Bravia TVs over the IRCC-IP protocol (still supported on Android TV / Google TV
/// Bravia sets via Settings > Network > Home Network Setup > IP Control > Simple IP Control with a
/// Pre-Shared Key - verified still documented/supported in 2025/2026). Uses the BraviaIRCCControl
/// NuGet package (netstandard2.0, still published) instead of reimplementing the IRCC/SOAP protocol.
/// </summary>
public sealed class SonyBraviaPackage : IPackage
{
    private IRCCController? _controller;

    public static void Main(string[] args) => PackageHost.Start<SonyBraviaPackage>(args);

    public void OnStart()
    {
        var hostname = PackageHost.GetSettingValue<string>("Hostname")!;
        var port = PackageHost.GetSettingValue<int>("Port");
        var pinCode = PackageHost.GetSettingValue<string>("PinCode")!;
        _controller = new IRCCController(hostname, port, pinCode);
        PackageHost.WriteInfo("Ready to control the Bravia device at {0}:{1}", hostname, port);
    }

    public void OnPreShutdown()
    {
    }

    public void OnShutdown()
    {
    }

    [MessageHandler(Description = "Sends an IRCC remote-control code to the Bravia device.")]
    public async Task<bool> SendIRCCCode(IRCCCodes code)
    {
        if (_controller == null)
        {
            PackageHost.WriteError("The controller isn't initialized yet");
            return false;
        }
        var success = await _controller.Send(code);
        if (!success)
        {
            PackageHost.WriteWarn("Failed to send the IRCC code '{0}'", code);
        }
        return success;
    }
}
