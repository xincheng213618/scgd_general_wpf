using ColorVision.Core;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorVision.Engine.Templates.Jsons.FOV2;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate;
using Xunit.Abstractions;

namespace ColorVision.UI.Tests;

public sealed class FovFieldImageFactAttribute : FactAttribute
{
    public FovFieldImageFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COLORVISION_FOV_FIELD_IMAGE")))
            Skip = "Set COLORVISION_FOV_FIELD_IMAGE to a local TIFF for read-only field verification.";
    }
}

[Collection(LuminousAreaNativeInteropCollection.CollectionName)]
public sealed class FovFieldImageTests(ITestOutputHelper output)
{
    [NativeV2Fact]
    public void DarkCornerGradientDoesNotBecomeGeometricFovEdge()
    {
        const int width = 1120, height = 820, left = 200, top = 250, right = 900, bottom = 600;
        byte[] pixels = new byte[width * height];
        for (int y = top; y < bottom; y++)
            for (int x = left; x < right; x++)
            {
                double darkness = Math.Clamp((x - 650) / 250d, 0, 1) * (y - top) / (bottom - top);
                pixels[y * width + x] = (byte)(220 * (1 - 0.88 * darkness));
            }
        GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            HImage image = new() { cols = width, rows = height, depth = 8, channels = 1, stride = width, pData = handle.AddrOfPinnedObject(), isDispose = true };
            FovCalculationResult result = FovCalculator.DetectAndCalculate(image, default, 9410, 74.2);
            Assert.Equal("RobustV2", result.Detection?.Algorithm);
            Assert.Equal(result.Detection!.Corners, result.Measurement.Corners);
            // Same 14 px localization contract as the native quadrilateral fixture.
            Assert.InRange(Math.Abs(result.Measurement.Corners[2].X - (right - 1)), 0, 14);
            Assert.InRange(Math.Abs(result.Measurement.Corners[2].Y - (bottom - 1)), 0, 14);
            LuminousAreaDetectionResult experimental = FovLuminousAreaDetector.Refine(image, default, result.Measurement.Corners, 0.5);
            Assert.True(experimental.Success, experimental.Diagnostic);
            Assert.True(experimental.Corners[2].X < right - 50, "The fixture must reproduce the internal half-brightness crossing.");
            FovCalculationResult upstream = FovCalculator.DetectAndCalculate(image, default, 9410, 74.2, coarseCorners: result.Measurement.Corners);
            Assert.Equal(result.Measurement.Corners, upstream.Measurement.Corners);
        }
        finally { handle.Free(); }
    }

    [FovFieldImageFact]
    public void FieldImageComparison()
    {
        string path = Environment.GetEnvironmentVariable("COLORVISION_FOV_FIELD_IMAGE")!;
        Stopwatch loadWatch = Stopwatch.StartNew();
        using LocalFlowFrame frame = LocalFrameFileService.Load(path);
        using LocalFlowFrameLease lease = frame.Acquire();
        HImage image = LocalFindLuminousAreaNode.CreateBorrowedImage(lease);
        loadWatch.Stop();
        Assert.Equal(9568, image.cols);
        Assert.Equal(6380, image.rows);
        List<object> runs = [];
        FovMeasurement? first = null;
        for (int iteration = 0; iteration < 3; iteration++)
        {
            Stopwatch watch = Stopwatch.StartNew();
            LuminousAreaDetectionResult robust = LuminousAreaNative.DetectV2(image, new RoiRect(), 0.25);
            double localizationMs = watch.Elapsed.TotalMilliseconds;
            Assert.True(robust.Success, robust.Diagnostic + robust.FailureReason);
            FovMeasurement direct = FovCalculator.Calculate(robust.Corners, 9410, 74.2);
            double directTotalMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            LuminousAreaDetectionResult boundary = FovLuminousAreaDetector.Refine(image, new RoiRect(), robust.Corners, 0.5, robust);
            double boundaryMs = watch.Elapsed.TotalMilliseconds;
            Assert.True(boundary.Success, boundary.Diagnostic);
            FovMeasurement refined = FovCalculator.Calculate(boundary.Corners, 9410, 74.2);
            watch.Restart();
            FovCalculationResult current = FovCalculator.DetectAndCalculate(image, new RoiRect(), 9410, 74.2);
            double fullMs = watch.Elapsed.TotalMilliseconds;
            Assert.Equal("RobustV2", current.Detection?.Algorithm);
            Assert.Equal(robust.Corners, current.Measurement.Corners);
            Assert.Empty(current.Detection!.Warnings);
            if (first != null) Assert.Equal(first.Corners, current.Measurement.Corners);
            first = current.Measurement;
            Assert.True(boundary.Corners[2].X < robust.Corners[2].X - 400, "Expected the reported lower-right dark-gradient failure on this fixture.");
            watch.Restart();
            FovCalculationResult reused = FovCalculator.DetectAndCalculate(new HImage(), default, 9410, 74.2, coarseCorners: robust.Corners);
            double reuseMs = watch.Elapsed.TotalMilliseconds;
            Assert.Equal(current.Measurement.Corners, reused.Measurement.Corners);
            runs.Add(new { iteration, localizationMs, directTotalMs, boundaryMs, fullMs, reuseMs, robust, direct, boundary, refined, current });
            if (iteration == 0 && Environment.GetEnvironmentVariable("COLORVISION_FOV_FIELD_REPORT") is string report)
            {
                RenderOverlay(path, current.Measurement, Path.ChangeExtension(report, ".geometric.png"));
                RenderOverlay(path, refined, Path.ChangeExtension(report, ".experimental.png"));
            }
        }
        string json = JsonConvert.SerializeObject(new { path, image.cols, image.rows, image.depth, image.channels, loadMs = loadWatch.Elapsed.TotalMilliseconds, runs }, Formatting.Indented);
        output.WriteLine(json);
        string? destination = Environment.GetEnvironmentVariable("COLORVISION_FOV_FIELD_REPORT");
        if (!string.IsNullOrWhiteSpace(destination)) File.WriteAllText(destination, json);
    }

    private static void RenderOverlay(string path, FovMeasurement measurement, string destination)
    {
        WpfTestHost.Invoke(() =>
        {
            FovCalculationTests.EnsureImageViewTestResources();
            using ImageView view = new();
            using FileStream sourceFile = File.OpenRead(path);
            BitmapSource source = BitmapDecoder.Create(sourceFile, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            view.SetImageSource(source, false, false);
            view.EditorContext.DrawEditorContext.Zoombox.ContentMatrix = new Matrix(0.5, 0, 0, 0.5, 0, 0);
            FovImageViewRunner.Render(view.EditorContext.ProcessingContext, view.EditorContext.DrawEditorContext, measurement);
            DrawingVisualBase[] overlays = view.ImageShow.Visuals.OfType<DrawingVisualBase>()
                .Where(v => Equals(v.BaseAttribute.Tag, AlgorithmResultOverlay.FovTag)).ToArray();
            Assert.Single(overlays.OfType<DVPolygon>());
            Assert.Equal(4, overlays.OfType<DVLine>().Count());
            Assert.Equal(7, overlays.OfType<DVCircleText>().Count());
            DrawingVisual drawing = new();
            using (DrawingContext dc = drawing.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, 1500, 750));
                dc.PushTransform(new ScaleTransform(0.5, 0.5));
                dc.PushTransform(new TranslateTransform(-3200, -2450));
                dc.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
                dc.PushTransform(new ScaleTransform(source.DpiX / 96, source.DpiY / 96));
                foreach (DrawingVisualBase overlay in overlays) dc.DrawDrawing(overlay.Drawing);
                dc.Pop(); dc.Pop(); dc.Pop();
            }
            RenderTargetBitmap bitmap = new(1500, 750, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(drawing);
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using FileStream file = File.Create(destination);
            encoder.Save(file);
        });
    }
}
