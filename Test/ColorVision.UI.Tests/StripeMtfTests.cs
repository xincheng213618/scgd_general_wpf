using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Algorithm.LocalMtf;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Algorithms.Mtf;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.Mtf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Xunit.Abstractions;

namespace ColorVision.UI.Tests;

public sealed class StripeMtfTests
{
    [Theory]
    [InlineData(StripeMtfPattern.Horizontal, false, 8)]
    [InlineData(StripeMtfPattern.Vertical, false, 16)]
    [InlineData(StripeMtfPattern.Horizontal, true, 16)]
    public void HAndVUseTheSameContrastFormulaAndPreserveLegacyShape(StripeMtfPattern pattern, bool percent, int bits)
    {
        using Mat pixels = new(80, 96, bits == 8 ? MatType.CV_8UC1 : MatType.CV_16UC1);
        for (int y = 0; y < 80; y++) for (int x = 0; x < 96; x++)
        {
            int value = ((pattern == StripeMtfPattern.Horizontal ? y : x) / 4) % 2 == 0 ? 40 : 200;
            if (bits == 8) pixels.Set(y, x, (byte)value); else pixels.Set(y, x, (ushort)(value * 100));
        }
        using Mat before = pixels.Clone();
        StripeMtfParameters p = new() { Pattern = pattern, PercentageDisplay = percent };
        JObject json = StripeMtfAnalyzer.Calculate(Image(pixels), p, [new("Center", 8, 8, 64, 64)]);
        MTFResult legacy = JsonConvert.DeserializeObject<MTFResult>(json.ToString())!;
        Assert.Equal(2.0 / 3 * (percent ? 100 : 1), Assert.Single(legacy.result).mtfValue!.Value, 12);
        Assert.Null(legacy.resultChild);
        Assert.Equal((int)pattern, json.Value<int>("resultType"));
        Assert.Equal(0, Cv2.Norm(before, pixels, NormTypes.INF));
        Assert.Equal(8, legacy.result[0].x);
    }

    [Fact]
    public void NonContiguousSixteenBitInputUsesItsRowStride()
    {
        using Mat allocation = new(20, 40, MatType.CV_16UC1, new Scalar(1000));
        using Mat input = new(allocation, new OpenCvSharp.Rect(3, 2, 20, 16));
        using (Mat bright = new(input, new OpenCvSharp.Rect(10, 0, 10, 16))) bright.SetTo(new Scalar(9000));
        Assert.False(input.IsContinuous());
        JObject result = StripeMtfAnalyzer.Calculate(Image(input), new() { Pattern = StripeMtfPattern.Vertical }, [new("P", 0, 0, 20, 16)]);
        Assert.Equal(.8, result["result"]![0]!.Value<double>("mtfValue"), 12);
        Assert.Equal((ushort)1000, allocation.At<ushort>(19, 39));
    }

    [Fact]
    public void QuantileTrimmingCountsPixelsRatherThanDiscardingEntireEqualValueBins()
    {
        using Mat pixels = new(20, 20, MatType.CV_16UC1, new Scalar(1000));
        using (Mat right = new(pixels, new OpenCvSharp.Rect(10, 0, 10, 20))) right.SetTo(new Scalar(9000));
        pixels.Set(0, 0, (ushort)0); pixels.Set(19, 19, ushort.MaxValue);
        foreach (StripeMtfMethod method in Enum.GetValues<StripeMtfMethod>())
        {
            JObject result = StripeMtfAnalyzer.Calculate(Image(pixels), new() { Pattern = StripeMtfPattern.Horizontal, Method = method }, [new("P", 0, 0, 20, 20)]);
            Assert.Equal(.8, result["result"]![0]!.Value<double>("mtfValue"), 12);
        }
    }

    [Fact]
    public void DarkInputIsRejectedAndConstantLitInputIsValidZero()
    {
        using Mat pixels = new(32, 32, MatType.CV_16UC1, new Scalar(0));
        StripeMtfParameters p = new() { Pattern = StripeMtfPattern.Vertical };
        Assert.Throws<InvalidOperationException>(() => StripeMtfAnalyzer.Calculate(Image(pixels), p, [new("P", 0, 0, 32, 32)]));
        pixels.SetTo(new Scalar(100));
        Assert.Equal(0, StripeMtfAnalyzer.Calculate(Image(pixels), p, [new("P", 0, 0, 32, 32)])["result"]![0]!.Value<double>("mtfValue"));
    }

    [Fact]
    public void FourPartGroupsOppositeCornersAndHonorsHorizontalAssignment()
    {
        using Mat pixels = new(400, 400, MatType.CV_16UC1, new Scalar(0));
        for (int y = 100; y < 300; y++) for (int x = 100; x < 300; x++)
        {
            bool firstPair = (x < 200) == (y < 200);
            bool bright = ((firstPair ? y : x) / 4) % 2 == 0;
            pixels.Set(y, x, (ushort)(firstPair ? (bright ? 10000 : 2000) : (bright ? 12000 : 4000)));
        }
        foreach (bool firstIsH in new[] { false, true })
        {
            StripeMtfParameters p = new() { RectWidth = 20, RectHeight = 20, FirstIsHorizontal = firstIsH };
            JObject result = StripeMtfAnalyzer.Calculate(Image(pixels), p, [new("C", 0, 0, 400, 400)]);
            Assert.Equal(4, result["result"]!.Count());
            JToken group = Assert.Single(result["resultChild"]!);
            Assert.Equal(firstIsH ? 2.0 / 3 : .5, group.Value<double>("horizontalAverage"), 12);
            Assert.Equal(firstIsH ? .5 : 2.0 / 3, group.Value<double>("verticalAverage"), 12);
            Assert.Equal(7.0 / 12, group.Value<double>("Average"), 12);
            Assert.Equal(Enumerable.Range(0, 4), group["childRects"]!.Select(r => r.Value<int>("id")));
        }
        Assert.Throws<InvalidOperationException>(() => StripeMtfAnalyzer.Calculate(Image(pixels),
            new() { OffsetX = int.MaxValue - 1000, RectWidth = int.MaxValue }, [new("C", 0, 0, 400, 400)]));
        pixels.SetTo(Scalar.All(0));
        Assert.Throws<InvalidOperationException>(() => StripeMtfAnalyzer.Calculate(Image(pixels), new(), [new("C", 0, 0, 400, 400)]));
    }

    [Fact]
    public void ParametersRoundTripWithoutDatabaseAndRejectUnsupportedCorrections()
    {
        StripeMtfParameters p = new() { Pattern = StripeMtfPattern.Vertical, WhiteNoiseRatio = .02, TailRatio = .2, PercentageDisplay = true };
        JObject saved = p.ToJson();
        p.ShowAdvanced = true;
        Assert.True(JToken.DeepEquals(saved, p.ToJson()));
        StripeMtfParameters restored = StripeMtfParameters.FromJson(p.ToJson().ToString());
        Assert.False(restored.ShowAdvanced);
        Assert.True(JToken.DeepEquals(saved, restored.ToJson()));
        Assert.Throws<ArgumentException>(() => StripeMtfParameters.FromJson("""{"sensorRatio":1.2}"""));
        Assert.Throws<ArgumentException>(() => StripeMtfParameters.FromJson("""{"dRatio":0.5}"""));
        p.Method = StripeMtfMethod.TrimmedExtrema; p.TailRatio = 0;
        p.Validate(); // An inactive, hidden sampling ratio does not block the extrema method.
        p.Method = StripeMtfMethod.TailMean;
        Assert.Throws<ArgumentException>(p.Validate);
    }

    [Fact]
    public void RoiBoundsAndNamesAreValidatedBeforePixelsAreRead()
    {
        StripeMtfAnalyzer.ValidateRegions([new("Full", 0, 0, 9568, 6380)], 9568, 6380);
        Assert.Throws<ArgumentException>(() => StripeMtfAnalyzer.ValidateRegions([new("P", 0, 0, 10, 10), new("P", 20, 20, 10, 10)], 100, 100));
        Assert.Throws<ArgumentException>(() => StripeMtfAnalyzer.ValidateRegions([new("P", int.MaxValue, 0, 10, 10)], 100, 100));
        Assert.Throws<ArgumentException>(() => StripeMtfAnalyzer.Calculate(default, new(), [new("P", 0, 0, 10, 10)]));
    }

    [Fact]
    public void ImageViewCoordinatesMenusAndResultLifecycleFollowTheImage()
    {
        WpfTestHost.Invoke(() =>
        {
            Application app = Application.Current;
            app.Resources["TextBox.Small"] = new Style(typeof(TextBox));
            app.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
            app.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
            app.Resources["ToolBarImage"] = new Style(typeof(System.Windows.Controls.Image));
            app.Resources["BaseStyle"] = new Style(typeof(Control));
            app.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
            app.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
            using ImageView view = new();
            var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(8, 8, 96, 96, System.Windows.Media.PixelFormats.Gray8, null, new byte[64], 8);
            view.SetImageSource(bitmap, enableEditorImageServices: false, configureDefaultLayerController: false);
            var editor = view.EditorContext; var image = editor.ProcessingContext; var draw = editor.DrawEditorContext;
            var rectangle = new RectangleProperties { Rect = new System.Windows.Rect(10.5, 11.5, 20, 30) };
            var roi = Assert.Single(StripeMtfImageViewRunner.Capture([rectangle], 192, 144));
            Assert.Equal(new MtfRoi("P_0", 21, 17, 40, 46), roi);
            Assert.Equal(AlgorithmMenuGroups.ImageQuality.Id, Assert.Single(new CMStripeMtf(editor).GetContextMenuItems()).OwnerGuid);
            Assert.Single(new DVCMStripeMtf(editor).GetContextMenuItems(rectangle));
            JObject json = JObject.Parse("""{"result":[{"name":"P","x":1,"y":2,"w":3,"h":4,"mtfValue":0.8}]}""");
            using AlgorithmResult result = StripeMtfImageViewRunner.CreateOverlay(json, false);
            using IDisposable overlay = AlgorithmOverlayRenderer.Apply(image, draw, result);
            long revision = image.ImageRevision; Guid document = image.DocumentInstanceId;
            long old = AlgorithmResultOverlay.BeginRequest(draw, StripeMtfImageViewRunner.OverlayName);
            Assert.True(StripeMtfImageViewRunner.IsCurrent(image, draw, document, revision, old));
            AlgorithmResultOverlay.BeginRequest(draw, StripeMtfImageViewRunner.OverlayName);
            Assert.False(StripeMtfImageViewRunner.IsCurrent(image, draw, document, revision, old));
            Assert.Single(image.SnapshotAlgorithmOverlayRegistrations());
            image.NotifySourcePixelsChanged();
            Assert.Empty(image.SnapshotAlgorithmOverlayRegistrations());
            StripeMtfResultWindow window = new(json, new() { Pattern = StripeMtfPattern.Horizontal }, 1);
            try
            {
                Assert.Equal(Visibility.Collapsed, Assert.IsType<TabItem>(window.FindName("GroupsTab")).Visibility);
                Assert.Equal(.8, Assert.Single(StripeMtfResultWindow.BuildRows(json)).Value);
            }
            finally { window.Close(); }
        });
    }

    internal static HImage Image(Mat mat) => new() { cols = mat.Cols, rows = mat.Rows, depth = mat.ElemSize1() * 8,
        channels = mat.Channels(), stride = checked((int)mat.Step()), pData = mat.Data };
}

[Collection("MTF offline comparison")]
public sealed class StripeMtfFieldTests(ITestOutputHelper output)
{
    [MtfFieldFact]
    public void CompareIndependentAlgorithmWithVendorOnTheSameResidentFrame()
    {
        JObject testCase = JObject.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("COLORVISION_MTF_FIELD_CASE")!));
        using LocalFlowFrame frame = LocalFrameFileService.Load(testCase.Value<string>("ImagePath")!);
        using LocalFlowFrameLease lease = frame.Acquire(); HImage image = LocalFindLuminousAreaNode.CreateBorrowedImage(lease);
        MtfRoi[] regions = testCase["RoiRects"]!.ToObject<MtfRoi[]>()!;
        JObject expected = (JObject)testCase["Expected"]!;
        StripeMtfParameters parameters = StripeMtfParameters.FromJson(testCase["Parameters"]!.ToString());
        JObject? actual = null; List<double> own = [], vendor = [];
        // Alternate order after warmup. File decoding and assertions are outside both timers.
        for (int run = -2; run < 20; run++)
        {
            void Own() { var t = Stopwatch.StartNew(); actual = StripeMtfAnalyzer.Calculate(image, parameters, regions); if (run >= 0) own.Add(t.Elapsed.TotalMilliseconds); }
            void Vendor() { var t = Stopwatch.StartNew(); _ = VendorMtfReference.Calculate(image, testCase.Value<string>("AlgorithmDirectory")!, (JObject)testCase["Parameters"]!, regions); if (run >= 0) vendor.Add(t.Elapsed.TotalMilliseconds); }
            if (run % 2 == 0) { Own(); Vendor(); } else { Vendor(); Own(); }
        }
        Assert.Equal(36, actual!["result"]!.Count()); Assert.Equal(9, actual["resultChild"]!.Count());
        MTFResult legacy = JsonConvert.DeserializeObject<MTFResult>(actual.ToString())!;
        Assert.Equal(36, legacy.result.Count); Assert.Equal(9, legacy.resultChild.Count);
        var comparison = expected["result"]!.Select(e =>
        {
            JToken a = actual["result"]!.Single(v => v.Value<string>("name") == e.Value<string>("name") && v.Value<int>("id") == e.Value<int>("id"));
            return new { name = e.Value<string>("name"), id = e.Value<int>("id"), dx = a.Value<int>("x") - e.Value<int>("x"), dy = a.Value<int>("y") - e.Value<int>("y"),
                expected = e.Value<double>("mtfValue"), actual = a.Value<double>("mtfValue"), difference = a.Value<double>("mtfValue") - e.Value<double>("mtfValue") };
        }).ToArray();
        var groups = expected["resultChild"]!.Select(e =>
        {
            JToken a = actual["resultChild"]!.Single(v => v.Value<string>("name") == e.Value<string>("name"));
            return new { name = e.Value<string>("name"), horizontalDifference = a.Value<double>("horizontalAverage") - e.Value<double>("horizontalAverage"),
                verticalDifference = a.Value<double>("verticalAverage") - e.Value<double>("verticalAverage"), averageDifference = a.Value<double>("Average") - e.Value<double>("Average") };
        }).ToArray();
        // The same known sampling windows isolate contrast from automatic location for H and V.
        MtfRoi[] fixedRegions = expected["result"]!.Select(r => new MtfRoi($"{r.Value<string>("name")}_{r.Value<int>("id")}",
            r.Value<int>("x"), r.Value<int>("y"), r.Value<int>("w"), r.Value<int>("h"))).ToArray();
        List<object> singlePatternComparisons = [];
        foreach (StripeMtfPattern pattern in new[] { StripeMtfPattern.Horizontal, StripeMtfPattern.Vertical })
        {
            StripeMtfParameters single = StripeMtfParameters.FromJson(parameters.ToJson().ToString()); single.Pattern = pattern;
            JObject singleResult = StripeMtfAnalyzer.Calculate(image, single, fixedRegions);
            JObject vendorOptions = (JObject)testCase["Parameters"]!.DeepClone(); vendorOptions["pattern"] = (int)pattern;
            JObject reference = VendorMtfReference.Calculate(image, testCase.Value<string>("AlgorithmDirectory")!, vendorOptions, fixedRegions);
            Assert.Equal(fixedRegions.Length, singleResult["result"]!.Count());
            double maxDifference = singleResult["result"]!.Zip(reference["result"]!, (a, b) => Math.Abs(a.Value<double>("mtfValue") - b.Value<double>("mtfValue"))).Max();
            singlePatternComparisons.Add(new { Pattern = pattern.ToString(), Count = fixedRegions.Length, MaxAbsoluteValueDifference = maxDifference });
        }
        var report = new { Formula = StripeMtfAnalyzer.FormulaVersion, ImageWidth = image.cols, ImageHeight = image.rows, Bits = image.depth, image.channels,
            InputRegions = regions.Length, WarmupRuns = 2, MeasuredRuns = 20, TimingScope = "Same resident RAW frame; excludes file decoding, persistence and UI",
            OwnMedianMs = own.Order().Skip(9).Take(2).Average(), VendorMedianMs = vendor.Order().Skip(9).Take(2).Average(),
            MaxAbsoluteValueDifference = comparison.Max(c => Math.Abs(c.difference)), MaxCoordinateDifference = comparison.Max(c => Math.Max(Math.Abs(c.dx), Math.Abs(c.dy))),
            OwnMilliseconds = own, VendorMilliseconds = vendor, Comparison = comparison, GroupComparison = groups, SinglePatternComparison = singlePatternComparisons, Result = actual };
        string json = JsonConvert.SerializeObject(report, Formatting.Indented);
        string? path = Environment.GetEnvironmentVariable("COLORVISION_MTF_OWN_REPORT");
        if (!string.IsNullOrWhiteSpace(path)) File.WriteAllText(path, json);
        output.WriteLine($"own={report.OwnMedianMs:F3}ms vendor={report.VendorMedianMs:F3}ms maxValueDifference={report.MaxAbsoluteValueDifference:G9} maxCoordinateDifference={report.MaxCoordinateDifference}px");
    }
}
