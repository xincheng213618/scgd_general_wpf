using cvColorVision;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace ColorVision.Engine.Services.Devices.Camera.Diagnostics;

internal sealed class HikCaptureTestSettings
{
    public const string LaunchArgument = "--hik-capture-test";
    public string CameraId { get; set; } = string.Empty;
    public float ExposureMs { get; set; } = 15.67f;
    public float Gain { get; set; }
    public string Culture { get; set; } = CultureInfo.CurrentUICulture.Name;
    public uint BayerQuality { get; set; } = 3;

    internal static void Launch(DeviceCamera device)
    {
        if (device.Config.CameraModel != CameraModel.HK_USB)
            throw new InvalidOperationException(EngineLocalization.Get("HikTest_HkOnly"));
        if (device.CameraBackend.LocalOwned || device.CameraBackend.VideoOwned || device.CameraBackend.ServiceMayOwnCamera)
            throw new InvalidOperationException(EngineLocalization.Get("HikTest_CloseCamera"));
        var settings = new HikCaptureTestSettings
        {
            CameraId = device.Config.CameraID ?? string.Empty,
            ExposureMs = (float)device.DisplayConfig.ExpTime,
            Gain = device.DisplayConfig.Gain,
            BayerQuality = (uint)device.DisplayConfig.HikBayerQuality
        };
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "ColorVision.Engine.exe"))
        {
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory
        };
        start.ArgumentList.Add(LaunchArgument);
        start.ArgumentList.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(settings))));
        using Process? process = Process.Start(start);
    }

    internal static HikCaptureTestSettings Parse(string[] arguments) => arguments.Length < 2
        ? new HikCaptureTestSettings()
        : JsonConvert.DeserializeObject<HikCaptureTestSettings>(Encoding.UTF8.GetString(Convert.FromBase64String(arguments[1])))
            ?? throw new InvalidOperationException("Invalid capture test settings.");
}
