using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.ImageEditor;
using log4net;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.ImageExport;

internal sealed class VidFocusImageExport(string root, bool saveByDate, string serialNumber, DateTime requestedAt)
{
    private static readonly ILog log = LogManager.GetLogger(typeof(VidFocusImageExport));
    // The existing plugin floor predates this host API. Older hosts still run the measurement.
    private static readonly Func<DeviceCamera, AutoFocusParam, CancellationToken, bool, MsgRecord>? FocusWithEvidence =
        typeof(DeviceCamera).GetMethod("AutoFocusLocallyWithEvidence", BindingFlags.Public | BindingFlags.Instance,
            [typeof(AutoFocusParam), typeof(CancellationToken), typeof(bool)])?
            .CreateDelegate<Func<DeviceCamera, AutoFocusParam, CancellationToken, bool, MsgRecord>>();

    private Func<string, double, CancellationToken, Task>? saveImages;

    internal static bool HostSupportsEvidence => FocusWithEvidence != null;

    internal static VidFocusImageExport? Capture(ViewResultManagerConfig config, string serialNumber) =>
        config.SaveVidFocusImages ? new(config.CsvSavePath, config.SaveByDate, serialNumber, DateTime.Now) : null;

    internal static MsgRecord Measure(DeviceCamera camera, AutoFocusParam template, CancellationToken token, bool fineFocus) =>
        FocusWithEvidence!(camera, template, token, fineFocus);

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal bool TryCaptureResult(MsgReturn response, out double? vid)
    {
        if (response.Data is CameraAutoFocusEvidence evidence)
        {
            vid = evidence.VidPosition;
            saveImages = (directory, correctedVid, token) => WriteImagesAsync(directory, evidence, correctedVid, token);
            return true;
        }
        vid = null;
        log.Warn("VID 对焦完成，未取得对焦图像。");
        return false;
    }

    internal async Task SaveAsync(double vid, CancellationToken token)
    {
        if (saveImages == null) return;
        try
        {
            string snDirectory = ProjectImageExportService.BuildOutputDirectory(root, saveByDate, requestedAt, serialNumber);
            string stamp = requestedAt.ToString("yyyyMMdd_HHmmss_fffffff", CultureInfo.InvariantCulture);
            string directory = Path.Combine(snDirectory, "VID_" + stamp);
            await saveImages(directory, vid, token);
            log.Info($"VID 对焦图像已保存: {directory}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Warn("VID 对焦图像保存失败。", ex);
        }
    }

    private static async Task WriteImagesAsync(string directory, CameraAutoFocusEvidence evidence, double vid, CancellationToken token)
    {
        using ImageViewSnapshot snapshot = CreateSnapshot(evidence, vid);
        await Task.Run(async () =>
        {
            token.ThrowIfCancellationRequested();
            string staging = directory + $".{Guid.NewGuid():N}.partial";
            string rendered = Path.Combine(staging, "VID.png");
            string curve = Path.Combine(staging, "FocusCurve.png");
            Directory.CreateDirectory(staging);
            try
            {
                await ImageView.SaveSnapshotAsync(snapshot, rendered, token);
                evidence.SaveCurvePng(curve, vid);
                token.ThrowIfCancellationRequested();
                Directory.Move(staging, directory);
            }
            finally
            {
                if (Directory.Exists(staging))
                {
                    File.Delete(rendered);
                    File.Delete(curve);
                    Directory.Delete(staging);
                }
            }
        }, token);
    }

    private static ImageViewSnapshot CreateSnapshot(CameraAutoFocusEvidence evidence, double vid)
    {
        int width = evidence.Frame.PixelWidth;
        int height = evidence.Frame.PixelHeight;
        double fontSize = Math.Max(12, Math.Min(width, height) / 28d);
        double padding = fontSize / 2;
        var label = new FormattedText($"VID {vid:R}\n最佳电机位置 {evidence.Position}", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), fontSize, Brushes.White, 1)
        {
            MaxTextWidth = Math.Max(1, width - 4 * padding)
        };
        var scene = new DrawingGroup();
        using (DrawingContext drawing = scene.Open())
        {
            drawing.DrawImage(evidence.Frame, new Rect(0, 0, width, height));
            drawing.DrawRectangle(Brushes.Black, null, new Rect(padding, padding, label.Width + 2 * padding, label.Height + 2 * padding));
            drawing.DrawText(label, new Point(2 * padding, 2 * padding));
        }
        return ImageViewSnapshot.Create(scene, width, height);
    }
}
