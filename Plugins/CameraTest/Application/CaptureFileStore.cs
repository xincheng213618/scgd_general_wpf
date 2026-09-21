using CameraTest.Models;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace CameraTest.Application;

public sealed record SavedCapture(string ImagePath, double SaveMilliseconds);

public static class CaptureFileStore
{
    public static SavedCapture Save(TestFrame frame, string directory, double captureMilliseconds)
    {
        var watch = Stopwatch.StartNew();
        Directory.CreateDirectory(directory);
        string name = $"CameraTest-{frame.Data.CapturedAt:yyyyMMdd-HHmmss-fff}-{frame.Id:N}";
        string imagePath = Path.Combine(directory, name + ".png");
        string metadataPath = Path.Combine(directory, name + ".json");
        bool createdImage = false, createdMetadata = false;
        try
        {
            using (var image = new FileStream(imagePath, FileMode.CreateNew))
            {
                createdImage = true;
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(frame.CreateBitmap()));
                encoder.Save(image);
            }
            using var metadata = new FileStream(metadataPath, FileMode.CreateNew);
            createdMetadata = true;
            JsonSerializer.Serialize(metadata, new
            {
                frame.Id, frame.Source, frame.SourceKind, frame.Data.CapturedAt,
                frame.Data.Width, frame.Data.Height, frame.Data.BitDepth, frame.Data.Channels,
                frame.AcquisitionSettings, CaptureMilliseconds = captureMilliseconds,
                Image = Path.GetFileName(imagePath)
            }, ProfileStore.JsonOptions);
            metadata.Flush();
            return new(imagePath, watch.Elapsed.TotalMilliseconds);
        }
        catch
        {
            // Both paths are generated for this capture; never leave a seemingly complete capture after failure.
            if (createdImage && File.Exists(imagePath)) File.Delete(imagePath);
            if (createdMetadata && File.Exists(metadataPath)) File.Delete(metadataPath);
            throw;
        }
    }

    public static void SavePng(TestFrame frame, string path)
    {
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(frame.CreateBitmap()));
        encoder.Save(stream);
    }
}
