using CameraTest.Models;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace CameraTest.Application;

public static class ProductionArchive
{
    public static string Save(TestFrame frame, FrameAnalysis? result, TestProfile profile, ArchiveSettings settings, FocusHistory? focusHistory = null)
    {
        settings.Validate();
        profile.Validate();
        if (result != null && result.FrameId != frame.Id) throw new InvalidOperationException("分析结果不属于当前图像，不能存档。");
        string root = Path.GetFullPath(settings.RootDirectory);
        string name = $"{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}";
        // Filenames do not contain device/operator input. Incomplete writes retain a distinct .pending directory.
        string pending = Path.Combine(root, name + ".pending");
        string destination = Path.Combine(root, name);
        Directory.CreateDirectory(pending);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(frame.CreateBitmap()));
        using (var stream = File.Create(Path.Combine(pending, "image.png"))) encoder.Save(stream);
        ProfileStore.Save(Path.Combine(pending, "profile.json"), profile);
        if (result != null)
        {
            result.Export(Path.Combine(pending, "results.json"));
            result.Export(Path.Combine(pending, "metrics.csv"));
        }
        if (focusHistory?.Count > 0) focusHistory.Export(Path.Combine(pending, "focus.csv"));
        var hashes = Directory.GetFiles(pending).ToDictionary(file => Path.GetFileName(file), file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))));
        var record = new
        {
            schemaVersion = 1, archivedAt = DateTimeOffset.Now, settings.DeviceSerial, settings.Operator, settings.Batch, settings.Notes,
            frame.Id, frame.Source, frame.SourceKind, imageTime = frame.Data.CapturedAt,
            imageTimeKind = frame.SourceKind == FrameSourceKind.ImageFile ? "imported_at" : "captured_at",
            frame.Data.Width, frame.Data.Height, frame.Data.BitDepth, frame.Data.Channels,
            requestedAcquisitionSettings = frame.AcquisitionSettings,
            acquisitionSettingsNote = frame.SourceKind == FrameSourceKind.ImageFile ? "原图片拍摄参数未知" : "记录 SDK 已请求设置，未经硬件回读确认",
            analysisStatus = result == null ? "not_run" : "completed", judgment = result?.Judgment.Status ?? "未分析",
            focusFrames = focusHistory?.Count ?? 0,
            softwareVersion = typeof(ProductionArchive).Assembly.GetName().Version?.ToString(),
            rawPixelSha256 = Convert.ToHexString(SHA256.HashData(frame.Data.Pixels)), filesSha256 = hashes
        };
        File.WriteAllText(Path.Combine(pending, "record.json"), JsonSerializer.Serialize(record, ProfileStore.JsonOptions));
        Directory.Move(pending, destination);
        return destination;
    }
}
