using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.ImageEditor;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Services.Devices.Camera;

/// <summary>Image captured at the selected focus position and curve from the same local autofocus command.</summary>
public sealed class CameraAutoFocusEvidence
{
    private readonly LocalAutoFocusResult result;
    private readonly IReadOnlyDictionary<int, double> samples;
    private readonly string deviceCode;
    private readonly string evaluation;

    internal CameraAutoFocusEvidence(BitmapSource frame, LocalAutoFocusResult result, IReadOnlyDictionary<int, double> samples,
        string deviceCode, string evaluation)
    {
        Frame = frame;
        this.result = result;
        this.samples = samples;
        this.deviceCode = deviceCode;
        this.evaluation = evaluation;
    }

    [JsonIgnore]
    public BitmapSource Frame { get; }
    public int Position => result.MotorPosition;
    public double? VidPosition => result.VidPosition;

    public void SaveCurvePng(string path, double vid) =>
        CameraFocusCurve.RenderPng(path, deviceCode, evaluation, samples, result with { VidPosition = vid });

    /// <summary>Writes the motor positions and sharpness values in capture order.</summary>
    public void SaveSamplesCsv(string path, CancellationToken stop)
    {
        stop.ThrowIfCancellationRequested();
        using var writer = new StreamWriter(path);
        writer.WriteLine("MotorPosition,Sharpness");
        foreach (var sample in samples)
        {
            stop.ThrowIfCancellationRequested();
            writer.WriteLine(FormattableString.Invariant($"{sample.Key},{sample.Value:R}"));
        }
    }

    internal string SaveLocalResult(string? directory, DateTime completedAt, CancellationToken stop)
    {
        stop.ThrowIfCancellationRequested();
        if (samples.Count == 0) throw new InvalidOperationException("An autofocus result requires at least one sample.");

        string deviceFolder = deviceCode;
        foreach (char invalid in Path.GetInvalidFileNameChars())
            deviceFolder = deviceFolder.Replace(invalid, '_');
        deviceFolder = deviceFolder.Trim().TrimEnd('.');
        if (deviceFolder.Length == 0) deviceFolder = "Camera";

        string root = string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "AutoFocus")
            : directory.Trim();
        string day = completedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string timestamp = completedAt.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        string parent = Path.GetFullPath(Path.Combine(root, deviceFolder, day));
        string staging = Path.Combine(parent, $".{Guid.NewGuid():N}.partial");
        string image = Path.Combine(staging, "FocusImage.png");
        string curve = Path.Combine(staging, "FocusCurve.png");
        string csv = Path.Combine(staging, "FocusSamples.csv");
        Directory.CreateDirectory(staging);
        try
        {
            using ImageViewSnapshot snapshot = CreateMarkedImage();
            ImageView.SaveSnapshotAsync(snapshot, image, stop).GetAwaiter().GetResult();
            stop.ThrowIfCancellationRequested();
            CameraFocusCurve.RenderPng(curve, deviceCode, evaluation, samples, result);
            SaveSamplesCsv(csv, stop);

            string output = Path.Combine(parent, timestamp);
            for (int suffix = 2; ; suffix++)
            {
                stop.ThrowIfCancellationRequested();
                try
                {
                    Directory.Move(staging, output);
                    return output;
                }
                catch (IOException) when (Directory.Exists(output) || File.Exists(output))
                {
                    output = Path.Combine(parent, FormattableString.Invariant($"{timestamp}_{suffix}"));
                }
            }
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                File.Delete(image);
                File.Delete(curve);
                File.Delete(csv);
                Directory.Delete(staging);
            }
        }
    }

    private ImageViewSnapshot CreateMarkedImage()
    {
        int width = Frame.PixelWidth, height = Frame.PixelHeight;
        double fontSize = Math.Max(12, Math.Min(width, height) / 28d);
        double padding = fontSize / 2;
        string text = CameraFocusCurve.BestReadout(result with { VidPosition = null });
        if (VidPosition is double vid) text += $"\nVID {vid:R}";
        var label = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei UI"), fontSize, Brushes.White, 1)
        {
            MaxTextWidth = Math.Max(1, width - 4 * padding)
        };
        var scene = new DrawingGroup();
        using (DrawingContext drawing = scene.Open())
        {
            drawing.DrawImage(Frame, new Rect(0, 0, width, height));
            drawing.DrawRectangle(Brushes.Black, null, new Rect(padding, padding, label.Width + 2 * padding, label.Height + 2 * padding));
            drawing.DrawText(label, new Point(2 * padding, 2 * padding));
        }
        return ImageViewSnapshot.Create(scene, width, height);
    }
}
