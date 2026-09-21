using ColorVision.Engine.Services.Devices.Camera.Local;
using System.IO;
using System.Text.Json;

namespace CameraTest.Models;

/// <summary>Local operating preferences, separate from image-specific POI templates.</summary>
public sealed class CameraOperationSettings
{
    public StandaloneCameraOptions Camera { get; set; } = new();
    public VideoAnalysisSettings Video { get; set; } = new();
    public bool SaveCapturedImages { get; set; }
    public string CaptureDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ColorVision", "CameraTest");

    public void Validate()
    {
        if (Camera == null || Video == null || !Enum.IsDefined(Camera.Model) || !Enum.IsDefined(Camera.Mode)
            || Camera.BitDepth is not (8 or 16) || !float.IsFinite(Camera.ExposureMilliseconds) || Camera.ExposureMilliseconds <= 0
            || !float.IsFinite(Camera.Gain) || Camera.Gain < 0 || string.IsNullOrWhiteSpace(CaptureDirectory) || !Path.IsPathFullyQualified(CaptureDirectory))
            throw new InvalidDataException("相机本地设置无效。");
        Video.Validate();
    }
}

public sealed class CameraOperationSettingsStore(string path)
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision", "Config", "CameraTest.settings.json");
    public string PathName { get; } = Path.GetFullPath(path);

    public CameraOperationSettings Load()
    {
        if (!File.Exists(PathName)) return new();
        if (new FileInfo(PathName).Length > 1024 * 1024) throw new InvalidDataException("相机本地设置超过 1 MiB。");
        var settings = JsonSerializer.Deserialize<CameraOperationSettings>(File.ReadAllText(PathName), ProfileStore.JsonOptions)
            ?? throw new InvalidDataException("相机本地设置为空。");
        settings.Validate();
        return settings;
    }

    public void Save(CameraOperationSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
        string temporary = PathName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, ProfileStore.JsonOptions));
            File.Move(temporary, PathName, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
