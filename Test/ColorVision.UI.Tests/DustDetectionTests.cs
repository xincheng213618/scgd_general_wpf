using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using OpenCvSharp;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed partial class DisplayMetrologyTests
{
    [Theory]
    [InlineData(AlgorithmImageFormat.Gray8)]
    [InlineData(AlgorithmImageFormat.Gray16)]
    [InlineData(AlgorithmImageFormat.Bgr48)]
    [InlineData(AlgorithmImageFormat.Gray32Float)]
    public async Task DustFindsInteriorSpotsWithoutCountingCircularRimOrMissingSector(AlgorithmImageFormat format)
    {
        using var image = Image(512, 512, (x, y, _) =>
        {
            if ((x - 256) * (x - 256) + (y - 256) * (y - 256) > 225 * 225 || x < 190 && y < 200) return 0.01;
            if ((x - 320) * (x - 320) + (y - 290) * (y - 290) < 9 * 9
                || (x - 230) * (x - 230) + (y - 355) * (y - 355) < 7 * 7) return 0.45;
            return 0.75;
        }, format);
        byte[] before = image.Data.ToArray();
        using var result = await Run(DisplayMetrologyIds.Dust, new DustDetectionParameters { BorderMargin = 12, BackgroundKernel = 121 }, image);
        Success(result);
        var rows = result.GetArtifact<AlgorithmTableArtifact>("灰尘候选")!.Rows;
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.True(row["原图X_px"].GetDouble() > 200));
        Assert.Equal(before, image.Data.ToArray());
        Assert.DoesNotContain(result.Artifacts.OfType<AlgorithmImageArtifact>(), a => a.Role == "primary");
        var mask = result.GetArtifact<AlgorithmImageArtifact>("有效成像区域")!.Image;
        Assert.Equal(0, mask.Data.Span[150 * mask.Stride + 170]);
        Assert.Equal(255, mask.Data.Span[290 * mask.Stride + 320]);
    }

    [Fact]
    public async Task DustReportsSourceCoordinatesAfterBoundedDownsampling()
    {
        using var image = Image(2048, 1024, (x, y, _) => (x - 1200) * (x - 1200) + (y - 520) * (y - 520) < 30 * 30 ? 0.3 : 0.7);
        using var result = await Run(DisplayMetrologyIds.Dust, new DustDetectionParameters
            { MaximumAnalysisDimension = 512, BorderMargin = 12, BackgroundKernel = 121 }, image);
        Success(result);
        var row = Assert.Single(result.GetArtifact<AlgorithmTableArtifact>("灰尘候选")!.Rows);
        Assert.InRange(row["原图X_px"].GetDouble(), 1160, 1185);
        Assert.InRange(row["原图Y_px"].GetDouble(), 480, 505);
        var geometry = result.GetArtifact<AlgorithmGeometryArtifact>("dust-regions")!.Geometries.Single(g => g.Id == "dust-1");
        Assert.Equal(row["原图X_px"].GetDouble(), geometry.Points[0].X);
        Assert.Equal(512, result.GetArtifact<AlgorithmImageArtifact>("有效成像区域")!.Image.Width);
    }

    [Fact]
    public async Task DustDistinguishesCleanIlluminatedFieldFromMissingSignal()
    {
        using var clean = Image(320, 320, (_, _, _) => 0.6);
        using var dark = Image(320, 320, (_, _, _) => 0);
        using var good = await Run(DisplayMetrologyIds.Dust, new DustDetectionParameters(), clean);
        using var bad = await Run(DisplayMetrologyIds.Dust, new DustDetectionParameters(), dark);
        Success(good);
        Assert.Empty(good.GetArtifact<AlgorithmTableArtifact>("灰尘候选")!.Rows);
        Failure(bad, "no_illuminated_region");
    }

    [Fact]
    public async Task DustRejectsInvalidFloatBeforeResizingAndExcessiveMargin()
    {
        using var invalid = Image(512, 320, (x, y, _) => x == 7 && y == 7 ? double.NaN : 0.6, AlgorithmImageFormat.Gray32Float);
        using var small = Image(64, 64, (_, _, _) => 0.6);
        using var badSignal = await Run(DisplayMetrologyIds.Dust, new DustDetectionParameters { MaximumAnalysisDimension = 256 }, invalid);
        using var badMargin = await Run(DisplayMetrologyIds.Dust, new DustDetectionParameters(), small);
        Failure(badSignal, "invalid_signal");
        Failure(badMargin, "analysis_region_too_small");
        Assert.False(new DustDetectionParameters { ContrastPercent = double.NaN }.Validate().IsValid);
        Assert.False(new DustDetectionParameters { BackgroundKernel = 400 }.Validate().IsValid);
        Assert.False(new DustDetectionParameters { MinimumArea = 20, MaximumArea = 10 }.Validate().IsValid);
    }

    [DustSiteFact]
    public async Task DustSiteImagesProduceReadOnlyProductionEvidence()
    {
        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("COLORVISION_DUST_SOURCE")!);
        string output = Path.GetFullPath(Environment.GetEnvironmentVariable("COLORVISION_DUST_OUTPUT")
            ?? throw new InvalidOperationException("Set COLORVISION_DUST_OUTPUT outside the source directory."));
        Assert.False(output.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual(root, output);
        Directory.CreateDirectory(output);
        var reports = new List<object>();
        string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(p => Path.GetExtension(p).Equals(".tif", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(p).Equals(".tiff", StringComparison.OrdinalIgnoreCase)).Order().ToArray();
        Assert.NotEmpty(files);
        foreach (string path in files)
        {
            string before = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            using Mat original = Cv2.ImDecode(File.ReadAllBytes(path), ImreadModes.Unchanged);
            using AlgorithmImageBuffer buffer = AlgorithmImageInterop.FromMat(original);
            using AlgorithmResult result = await Run(DisplayMetrologyIds.Dust, new DustDetectionParameters(), buffer);
            Success(result);
            string relative = Path.GetRelativePath(root, path);
            string target = Path.Combine(output, Path.GetDirectoryName(relative)!, Path.GetFileNameWithoutExtension(relative));
            Directory.CreateDirectory(target);
            var rows = result.GetArtifact<AlgorithmTableArtifact>("灰尘候选")!.Rows;
            var context = result.GetArtifact<AlgorithmStructuredDataArtifact>("dust-analysis")!.Data;
            File.WriteAllText(Path.Combine(target, "result.json"), JsonSerializer.Serialize(new { file = relative, sourceSha256 = before, analysis = context, candidates = rows }, new JsonSerializerOptions { WriteIndented = true }));
            foreach (var artifact in result.Artifacts.OfType<AlgorithmImageArtifact>())
            {
                using Mat mat = AlgorithmImageInterop.ToMat(artifact.Image);
                Cv2.ImEncode(".png", mat, out byte[] bytes);
                File.WriteAllBytes(Path.Combine(target, artifact.Name + ".png"), bytes);
            }
            using Mat thumb = new();
            double scale = 1600.0 / original.Width;
            Cv2.Resize(original, thumb, new Size(1600, (int)Math.Round(original.Height * scale)), interpolation: InterpolationFlags.Area);
            using Mat view = new();
            Cv2.Normalize(thumb, view, 0, 255, NormTypes.MinMax);
            view.ConvertTo(view, MatType.CV_8U);
            if (view.Channels() == 1) Cv2.CvtColor(view, view, ColorConversionCodes.GRAY2BGR);
            foreach (var row in rows)
            {
                int x = (int)Math.Round(row["原图X_px"].GetDouble() * scale), y = (int)Math.Round(row["原图Y_px"].GetDouble() * scale);
                int w = Math.Max(4, (int)Math.Round(row["原图宽_px"].GetDouble() * scale)), h = Math.Max(4, (int)Math.Round(row["原图高_px"].GetDouble() * scale));
                Cv2.Rectangle(view, new Rect(x - 3, y - 3, w + 6, h + 6), new Scalar(40, 40, 255), 2);
                Cv2.PutText(view, row["编号"].ToString(), new Point(x, y - 7), HersheyFonts.HersheySimplex, 0.45, new Scalar(40, 40, 255), 1);
            }
            Cv2.ImEncode(".jpg", view, out byte[] preview);
            File.WriteAllBytes(Path.Combine(target, "overlay.jpg"), preview);
            string after = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            Assert.Equal(before, after);
            reports.Add(new { file = relative, candidateCount = rows.Count, sourceSha256 = before, sourceUnchanged = before == after });
        }
        File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public async Task DustImageViewMenuAndResultOverlayUseExistingAnalysisLifecycle()
    {
        using var input = Image(320, 320, (x, y, _) => (x - 170) * (x - 170) + (y - 160) * (y - 160) < 9 * 9 ? 0.3 : 0.7,
            AlgorithmImageFormat.Gray8);
        using var result = await Run(DisplayMetrologyIds.Dust, new DustDetectionParameters(), input);
        Success(result);
        WpfTestHost.Invoke(() =>
        {
            EnsureResources();
            using var view = new ImageView(ImageAlgorithmPlatform.Runtime);
            view.SetImageSource(ImageAlgorithmInputFactory.ToWriteableBitmap(input), enableEditorImageServices: false, configureDefaultLayerController: false);
            var context = view.EditorContext.ProcessingContext;
            var menu = new AlgorithmsContextMenu(context).GetContextMenuItems();
            var entry = Assert.Single(menu, e => e.GuidId == DisplayMetrologyIds.Dust.Value);
            Assert.Equal(AlgorithmMenuGroups.Defects.Id, entry.OwnerGuid);
            Assert.True(entry.Command!.CanExecute(null));
            int before = context.ImageShow.Visuals.Count;
            using var window = new DisplayMetrologyResultWindow(result, "灰尘检测测试", context, view.EditorContext.DrawEditorContext);
            window.Show();
            Assert.Equal(before + 1, context.ImageShow.Visuals.Count);
            Assert.Single(context.AlgorithmOverlays.Snapshot());
            window.Close();
            Assert.Equal(before, context.ImageShow.Visuals.Count);
            Assert.Empty(context.AlgorithmOverlays.Snapshot());
            Assert.True(result.IsDisposed);
        });
    }
}

public sealed class DustSiteFactAttribute : FactAttribute
{
    public DustSiteFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COLORVISION_DUST_SOURCE")))
            Skip = "Set COLORVISION_DUST_SOURCE and COLORVISION_DUST_OUTPUT to run read-only image validation.";
    }
}
