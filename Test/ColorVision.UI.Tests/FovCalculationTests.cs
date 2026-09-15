using ColorVision.Core;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.FOV2;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit.Abstractions;

namespace ColorVision.UI.Tests;

public sealed class FovBoundaryPerformanceFactAttribute : FactAttribute
{
    public const string OptInVariable = "COLORVISION_RUN_FOV_BOUNDARY_PERF_TESTS";

    public FovBoundaryPerformanceFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "1", StringComparison.Ordinal))
            Skip = $"Set {OptInVariable}=1 to run the 9568x6380 FOV boundary performance probe.";
    }
}

public sealed class FovCalculationTests
{
    private readonly ITestOutputHelper output;

    public FovCalculationTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    private static readonly LuminousAreaPoint[] ServiceSampleCorners =
    [
        new(3383, 2656),
        new(6063, 2655),
        new(6056, 3717),
        new(3381, 3731)
    ];

    [Fact]
    public void ServiceSampleUsesExactDegreeConversion()
    {
        FovMeasurement result = FovCalculator.Calculate(ServiceSampleCorners, 9410, 74.2);

        Assert.InRange(Math.Abs(24.2891902198502 - result.HorizontalFovDegrees), 0, 0.000001);
        Assert.InRange(Math.Abs(9.81677705571827 - result.VerticalFovDegrees), 0, 0.000001);
        Assert.InRange(Math.Abs(26.0901534677615 - result.DiagonalFovDegrees), 0, 0.000001);
        Assert.InRange(Math.Abs(26.1509822353982 - result.LeftDownToRightUpDegrees), 0, 0.000001);
        Assert.InRange(Math.Abs(26.0293247001249 - result.LeftUpToRightDownDegrees), 0, 0.000001);
    }

    [Fact]
    public void FormulaUsesExactRadiansToDegreesConversion()
    {
        double expected = 2 * Math.Atan(2300.01 / 9410 * Math.Tan(74.2 / 2 * Math.PI / 180)) * 180 / Math.PI;
        Assert.Equal(expected, FovCalculator.PixelDistanceToDegrees(2300.01, 9410, 74.2), 12);
        Assert.Equal(20.9464, expected, 3);
    }

    [Fact]
    public void CameraDegreesCalculatorUsesSensorAndFocalLengthFormula()
    {
        Assert.Equal(74.1649939303433, FovCalculator.SensorLengthToDegrees(13, 8.6), 12);
        Assert.Equal(6221.12515311606, FovCalculator.EquivalentFocalLengthPixels(9410, 74.2), 9);

        CameraDegreesCalculatorOptions options = new(74.2, 9410)
        {
            SensorWidthMillimeters = 13,
            SensorHeightMillimeters = 8,
            EffectiveFocalLengthMillimeters = 8.6
        };

        Assert.Equal(options.HorizontalFovDegrees, options.SelectedCameraDegrees);
        options.ReferenceDirection = CameraFovReferenceDirection.Vertical;
        Assert.Equal(options.VerticalFovDegrees, options.SelectedCameraDegrees);
        options.ReferenceDirection = CameraFovReferenceDirection.Diagonal;
        Assert.Equal(options.DiagonalFovDegrees, options.SelectedCameraDegrees);
        Assert.True(options.TryGetSelectedCameraDegrees(out double selected, out string error));
        Assert.Equal(options.DiagonalFovDegrees, selected);
        Assert.Empty(error);
    }

    [Fact]
    public void CameraDegreesCalculatorExposesDistinctInputsAndLiveResults()
    {
        PropertyInfo[] properties = PropertyEditorHelper.GetEditableProperties(typeof(CameraDegreesCalculatorOptions));
        string[] propertyNames = properties.Select(property => property.Name).ToArray();

        Assert.Contains(nameof(CameraDegreesCalculatorOptions.HorizontalFovDegrees), propertyNames);
        Assert.Contains(nameof(CameraDegreesCalculatorOptions.VerticalFovDegrees), propertyNames);
        Assert.Contains(nameof(CameraDegreesCalculatorOptions.DiagonalFovDegrees), propertyNames);
        Assert.Contains(nameof(CameraDegreesCalculatorOptions.SelectedCameraDegrees), propertyNames);
        Assert.Equal("传感器有效宽度 (mm)", typeof(CameraDegreesCalculatorOptions)
            .GetProperty(nameof(CameraDegreesCalculatorOptions.SensorWidthMillimeters))!
            .GetCustomAttribute<DisplayNameAttribute>()!.DisplayName);
        Assert.Equal("传感器有效高度 (mm)", typeof(CameraDegreesCalculatorOptions)
            .GetProperty(nameof(CameraDegreesCalculatorOptions.SensorHeightMillimeters))!
            .GetCustomAttribute<DisplayNameAttribute>()!.DisplayName);

        CameraDegreesCalculatorOptions options = new(72, 9410)
        {
            SensorWidthMillimeters = 14,
            SensorHeightMillimeters = 8,
            EffectiveFocalLengthMillimeters = 14
        };
        Assert.NotNull(options.HorizontalFovDegrees);
        Assert.NotNull(options.VerticalFovDegrees);
        Assert.NotNull(options.DiagonalFovDegrees);
        Assert.Equal(options.HorizontalFovDegrees, options.SelectedCameraDegrees);

        PropertyEditSession session = PropertyEditSession.Create(options, PropertyEditorEditMode.Transactional);
        CameraDegreesCalculatorOptions editable = Assert.IsType<CameraDegreesCalculatorOptions>(session.EditableObject);
        editable.SensorWidthMillimeters = 16;
        Assert.NotEqual(options.HorizontalFovDegrees, editable.HorizontalFovDegrees);
        Assert.Equal(editable.HorizontalFovDegrees, editable.SelectedCameraDegrees);
        session.Commit();
        Assert.Equal(editable.HorizontalFovDegrees, options.HorizontalFovDegrees);
    }

    [Fact]
    public void LegacyJsonKeepsAllSevenHistoricalFieldsAndCasing()
    {
        FovMeasurement measurement = FovCalculator.Calculate(ServiceSampleCorners, 9410, 74.2);
        JObject result = JObject.Parse(LocalFovResultPersistence.BuildLegacyResultJson(measurement))["result"]!.Value<JObject>()!;

        Assert.Equal(
            ["D_Fov", "H_Fov", "V_FOV", "clolorVisionH_Fov", "clolorVisionV_Fov", "leftDownToRightUp", "leftUpToRightDown", "message"],
            result.Properties().Select(property => property.Name));
        Assert.Equal("Success", result.Value<string>("message"));
        ResDFov historicalReader = result.Root.ToObject<ResDFov>()!;
        Assert.Equal(measurement.DirectionalHorizontalFovDegrees, historicalReader.result.ClolorVisionH_Fov);
        Assert.Equal(measurement.DirectionalVerticalFovDegrees, historicalReader.result.ClolorVisionV_Fov);
        Assert.Equal(measurement.DiagonalFovDegrees, historicalReader.result.D_Fov);
    }

    [Fact]
    public void PersistenceKeepsHistoricalFovMasterAndSingleResultFileDetail()
    {
        LocalFovPersistenceRequest request = new()
        {
            BatchId = 27,
            ImageFilePath = @"C:\Samples\fov.cvraw",
            AlgorithmDeviceCode = "Algorithm1",
            ZIndex = 3,
            TotalTime = 18,
            Parameters = new { cameraDegrees = 74.2 }
        };

        AlgResultMasterModel master = LocalFovResultPersistence.CreateMasterModel(request);
        DetailCommonModel detail = LocalFovResultPersistence.CreateDetail(45, @"C:\Results\fov.json");
        ResultFile resultFile = JObject.Parse(detail.ResultJson!).ToObject<ResultFile>()!;

        Assert.Equal(ViewResultAlgType.FOV, master.ImgFileType);
        Assert.Equal("2.0", master.version);
        Assert.Equal(27, master.BatchId);
        Assert.Equal(3, master.Zindex);
        Assert.Equal("Algorithm1", master.DeviceCode);
        Assert.Equal(45, detail.PId);
        Assert.Equal(@"C:\Results\fov.json", resultFile.ResultFileName);
    }

    [Fact]
    public void FlowNodeExposesFovParametersWithApprovedDefaults()
    {
        LocalFovNode node = new();
        node.Create();

        Assert.Equal("LocalFOV", node.NodeType);
        Assert.Equal("FOV计算", node.Title);
        Assert.Equal(["IN"], node.GetAllInputOptions().Select(option => option.Text));
        Assert.Equal(["OUT"], node.GetAllOutputOptions().Select(option => option.Text));
        Assert.Equal(9410, node.FovDist);
        Assert.Equal(74.2, node.CameraDegrees);
        Assert.Equal(0.5, node.LuminanceBoundaryRatio);
        Assert.Null(typeof(LocalFovNode).GetProperty("DarkRatio", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(LocalFovNode).GetProperty("Threshold", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void ImageViewExposesFovParametersWithApprovedDefaults()
    {
        FovImageViewOptions options = new();
        Assert.Equal(9410, options.FovDist);
        Assert.Equal(74.2, options.CameraDegrees);
        Assert.Equal(0.25, options.MinimumConfidence);
        Assert.Equal(0.5, options.LuminanceBoundaryRatio);
        Assert.Null(typeof(FovImageViewOptions).GetProperty("CameraDeviceCode"));
        PropertyInfo minimumConfidence = typeof(FovImageViewOptions).GetProperty(nameof(FovImageViewOptions.MinimumConfidence))!;
        Assert.Equal(typeof(SliderPropertiesEditor), minimumConfidence.GetCustomAttribute<PropertyEditorTypeAttribute>()?.EditorType);
        Assert.False(TypeDescriptor.GetProperties(options)[nameof(options.LuminanceBoundaryRatio)]!.IsBrowsable);
        Assert.False(TypeDescriptor.GetProperties(typeof(LocalFovNode))[nameof(LocalFovNode.LuminanceBoundaryRatio)]!.IsBrowsable);
        PropertyEditorTypeAttribute? editor = typeof(FovImageViewOptions)
            .GetProperty(nameof(FovImageViewOptions.CameraDegrees))!
            .GetCustomAttribute<PropertyEditorTypeAttribute>();
        Assert.Equal(typeof(CameraDegreesPropertiesEditor), editor?.EditorType);
        Assert.Equal(typeof(CameraDegreesPropertiesEditor), typeof(LocalFovNode).GetProperty(nameof(LocalFovNode.CameraDegrees))!.GetCustomAttribute<PropertyEditorTypeAttribute>()?.EditorType);
    }

    [Fact]
    public void ImageViewResultMessageDoesNotMentionCameraBatchOrDatabase()
    {
        FovImageViewRunResult result = new()
        {
            Calculation = FovCalculator.CalculateFromCorners(ServiceSampleCorners, 9410, 74.2),
            TotalTime = 18
        };

        string message = FovImageViewRunner.BuildResultMessage(result);

        Assert.DoesNotContain("相机:", message, StringComparison.Ordinal);
        Assert.DoesNotContain("批次", message, StringComparison.Ordinal);
        Assert.DoesNotContain("数据库", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.1)]
    [InlineData(double.NaN)]
    public void ProvidedCornersDoNotRequirePixelsOrUseRetiredRatio(double oldRatio)
    {
        FovCalculationResult result = FovCalculator.DetectAndCalculate(
            new HImage(), default, 9410, 74.2, luminanceBoundaryRatio: oldRatio, coarseCorners: ServiceSampleCorners);
        Assert.Equal(ServiceSampleCorners, result.Measurement.Corners);
        Assert.True(result.UsedProvidedCorners);
        Assert.Null(result.Detection);
        Assert.Null(result.CoarseDetection);
        Assert.Equal("UpstreamCorners", result.BoundaryMode);
        Assert.Equal(FovCalculator.Calculate(ServiceSampleCorners, 9410, 74.2).DiagonalFovDegrees, result.Measurement.DiagonalFovDegrees);
    }

    [Fact]
    public void RetiredRatioStillLoadsFromSavedFlowNode()
    {
        LocalFovNode node = new();
        node.Create();
        node.OnLoadNode(new Dictionary<string, byte[]>
        {
            [nameof(LocalFovNode.LuminanceBoundaryRatio)] = System.Text.Encoding.UTF8.GetBytes(0.42.ToString(System.Globalization.CultureInfo.CurrentCulture))
        });
        Assert.Equal(0.42, node.LuminanceBoundaryRatio);
        Assert.False(TypeDescriptor.GetProperties(node)[nameof(node.LuminanceBoundaryRatio)]!.IsBrowsable);
    }

    [Fact]
    public void FovLuminanceBoundaryRefinesCoarseCornersRepeatably()
    {
        const int width = 240;
        const int height = 140;
        byte[] pixels = new byte[width * height];
        for (int y = 30; y < 110; y++)
        {
            for (int x = 40; x < 200; x++) pixels[y * width + x] = 200;
        }
        IntPtr pointer = Marshal.AllocCoTaskMem(pixels.Length);
        Marshal.Copy(pixels, 0, pointer, pixels.Length);
        HImage image = new()
        {
            rows = height,
            cols = width,
            channels = 1,
            depth = 8,
            stride = width,
            isDispose = true,
            pData = pointer
        };
        LuminousAreaPoint[] coarseCorners =
        [
            new(35, 25),
            new(204, 25),
            new(204, 114),
            new(35, 114)
        ];

        try
        {
            LuminousAreaPoint[]? firstCorners = null;
            for (int iteration = 0; iteration < 10; iteration++)
            {
                LuminousAreaDetectionResult result = FovLuminousAreaDetector.Refine(
                    image, new RoiRect(), coarseCorners, 0.5);

                Assert.True(result.Success, result.Diagnostic);
                Assert.Equal(FovLuminousAreaDetector.AlgorithmName, result.Algorithm);
                Assert.Equal(4, result.Corners.Count);
                if (firstCorners == null)
                    firstCorners = result.Corners.ToArray();
                else
                    Assert.Equal(firstCorners, result.Corners);
            }

            Assert.InRange(firstCorners![0].X, 38.5, 40.5);
            Assert.InRange(firstCorners[0].Y, 28.5, 30.5);
            Assert.InRange(firstCorners[2].X, 198.5, 200.5);
            Assert.InRange(firstCorners[2].Y, 108.5, 110.5);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    [Fact]
    public void NonStraightButCompleteBoundaryReturnsMeasurementWithWarnings()
    {
        const int width = 400;
        const int height = 240;
        byte[] pixels = new byte[width * height];
        for (int y = 40; y < 200; y++)
        {
            double phase = (y - 40) / 160d * Math.PI * 4;
            int offset = (int)Math.Round(18 * Math.Sin(phase));
            int left = 60 + offset;
            int right = 340 + offset;
            pixels.AsSpan(y * width + left, right - left).Fill(200);
        }

        GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        HImage image = new()
        {
            rows = height,
            cols = width,
            channels = 1,
            depth = 8,
            stride = width,
            isDispose = true,
            pData = handle.AddrOfPinnedObject()
        };
        LuminousAreaPoint[] coarseCorners =
        [
            new(40, 25),
            new(360, 25),
            new(360, 215),
            new(40, 215)
        ];

        try
        {
            LuminousAreaDetectionResult result = FovLuminousAreaDetector.Refine(
                image, new RoiRect(), coarseCorners, 0.5);

            Assert.True(result.Success, result.Diagnostic);
            Assert.True(result.HasValidCorners);
            Assert.Empty(result.FailureReason);
            Assert.NotEmpty(result.Warnings);
            Assert.Contains(result.Warnings, warning => warning is "WeakRightBoundary" or "WeakLeftBoundary");
            Assert.True(result.Confidence < 0.25, result.Diagnostic);
        }
        finally
        {
            handle.Free();
        }
    }

    [Fact]
    public void ResultImagePathUsesKnownSourceOnlyWhileItMatchesPrimaryFrame()
    {
        const string sourcePath = @"C:\capture\White51.png";
        using LocalFlowFrame sourceFrame = LocalFlowFrame.Allocate(
            new LocalFrameMetadata
            {
                Width = 1,
                Height = 1,
                SourceBpp = 8,
                Channels = 1,
                SourceFilePath = sourcePath,
                PrimaryBufferKind = LocalFrameBufferKind.CvRaw
            },
            rawLength: 1,
            cieLength: 0);

        Assert.Equal(sourcePath, sourceFrame.ResolveResultImageFilePath());

        sourceFrame.CvRawFilePath = @"C:\capture\saved.cvraw";
        Assert.Equal(sourceFrame.CvRawFilePath, sourceFrame.ResolveResultImageFilePath());

        using LocalFlowFrame calibratedFrame = LocalFlowFrame.Allocate(
            new LocalFrameMetadata
            {
                Width = 1,
                Height = 1,
                SourceBpp = 8,
                Channels = 3,
                SourceFilePath = sourcePath,
                CalibrationTemplate = "cal-a",
                PrimaryBufferKind = LocalFrameBufferKind.CvCie
            },
            rawLength: 0,
            cieLength: sizeof(float) * 3);

        Assert.Null(calibratedFrame.ResolveResultImageFilePath());
    }

    [FovBoundaryPerformanceFact]
    [Trait("Category", "PerformanceProbe")]
    public void FovBoundaryRefinementProbeAtCameraResolution()
    {
        const int width = 9568;
        const int height = 6380;
        const int left = 1500;
        const int top = 1000;
        const int right = 8068;
        const int bottom = 5380;
        byte[] pixels = new byte[checked(width * height)];
        for (int y = top; y < bottom; y++)
            pixels.AsSpan(y * width + left, right - left).Fill(200);

        GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        HImage image = new()
        {
            rows = height,
            cols = width,
            channels = 1,
            depth = 8,
            stride = width,
            isDispose = true,
            pData = handle.AddrOfPinnedObject()
        };
        LuminousAreaPoint[] coarseCorners =
        [
            new(left - 50, top - 50),
            new(right + 50, top - 50),
            new(right + 50, bottom + 50),
            new(left - 50, bottom + 50)
        ];

        try
        {
            Stopwatch firstWatch = Stopwatch.StartNew();
            LuminousAreaDetectionResult first = FovLuminousAreaDetector.Refine(
                image, new RoiRect(), coarseCorners, 0.5);
            firstWatch.Stop();
            Assert.True(first.Success, first.Diagnostic);

            Stopwatch repeatedWatch = Stopwatch.StartNew();
            for (int iteration = 0; iteration < 5; iteration++)
            {
                LuminousAreaDetectionResult result = FovLuminousAreaDetector.Refine(
                    image, new RoiRect(), coarseCorners, 0.5);
                Assert.True(result.Success, result.Diagnostic);
                Assert.Equal(first.Corners.ToArray(), result.Corners.ToArray());
            }
            repeatedWatch.Stop();

            output.WriteLine(
                $"9568x6380 refinement: first={firstWatch.Elapsed.TotalMilliseconds:F1}ms; " +
                $"five-repeat-total={repeatedWatch.Elapsed.TotalMilliseconds:F1}ms; " +
                $"repeat-average={repeatedWatch.Elapsed.TotalMilliseconds / 5:F1}ms");
            AssertCornersNear(first.Corners[0], new LuminousAreaPoint(left, top), 1.5);
            AssertCornersNear(first.Corners[2], new LuminousAreaPoint(right - 1, bottom - 1), 1.5);

            if (string.Equals(
                Environment.GetEnvironmentVariable(NativeV2FactAttribute.OptInVariable),
                "1",
                StringComparison.Ordinal))
            {
                Stopwatch fullWatch = Stopwatch.StartNew();
                FovCalculationResult full = FovCalculator.DetectAndCalculate(
                    image,
                    new RoiRect(),
                    9410,
                    74.2,
                    minimumConfidence: 0.2,
                    luminanceBoundaryRatio: 0.5);
                fullWatch.Stop();

                Assert.Null(full.CoarseDetection);
                Assert.Equal("RobustV2", full.Detection?.Algorithm);
                // The full path now preserves native geometry, rather than testing
                // the retired half-brightness subpixel boundary's 2 px tolerance.
                Assert.Equal(full.Detection!.Corners, full.Measurement.Corners);
                output.WriteLine($"9568x6380 full RobustV2+FOV: {fullWatch.Elapsed.TotalMilliseconds:F1}ms");
            }
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(double.NaN)]
    public void ExperimentalBoundaryRejectsInvalidRatios(double ratio)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FovLuminousAreaDetector.ValidateBoundaryRatio(ratio));
    }

    [Fact]
    public void CameraDegreesEditorPlacesCalculatorButtonAfterNumericInput()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            FovImageViewOptions options = new();
            PropertyInfo property = typeof(FovImageViewOptions).GetProperty(nameof(FovImageViewOptions.CameraDegrees))!;
            DockPanel panel = new CameraDegreesPropertiesEditor().GenProperties(property, options);

            Button button = Assert.Single(panel.Children.OfType<Button>());
            TextBox textBox = Assert.Single(panel.Children.OfType<TextBox>());
            Assert.Equal(Dock.Right, DockPanel.GetDock(button));
            Assert.Equal("计算 cameraDegrees", System.Windows.Automation.AutomationProperties.GetName(button));
            Assert.Equal("计算", button.Content);
            Assert.NotNull(textBox.GetBindingExpression(TextBox.TextProperty));
        });
    }

    [Fact]
    public void ImageViewRendererDrawsQuadrilateralAxesDiagonalsAndLabels()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            using ImageView view = new();
            view.SetImageSource(new WriteableBitmap(100, 100, 96, 96, PixelFormats.Gray8, null), false, false);
            FovMeasurement measurement = FovCalculator.Calculate(ServiceSampleCorners, 9410, 74.2);

            FovImageViewRunner.Render(view.EditorContext.ProcessingContext, view.EditorContext.DrawEditorContext, measurement);
            // A repeated invocation replaces, rather than accumulates, the FOV overlay.
            FovImageViewRunner.Render(view.EditorContext.ProcessingContext, view.EditorContext.DrawEditorContext, measurement);

            DrawingVisualBase[] overlays = view.ImageShow.Visuals
                .OfType<DrawingVisualBase>()
                .Where(visual => Equals(visual.BaseAttribute.Tag, AlgorithmResultOverlay.FovTag))
                .ToArray();
            Assert.Single(overlays.OfType<DVPolygon>());
            Assert.Equal(4, overlays.OfType<DVLine>().Count());
            Assert.Equal(7, overlays.OfType<DVCircleText>().Count());
        });
    }

    internal static void EnsureImageViewTestResources()
    {
        Application application = Application.Current ?? new Application();
        application.Resources["GlobalTextBrush"] = Brushes.Black;
        application.Resources["GlobalBorderBrush"] = Brushes.Gray;
        application.Resources["BorderBrush"] = Brushes.Gray;
        application.Resources["ButtonCommand"] = new Style(typeof(Button));
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }

    private static void AssertCornersNear(LuminousAreaPoint actual, LuminousAreaPoint expected, double tolerance)
    {
        double dx = actual.X - expected.X;
        double dy = actual.Y - expected.Y;
        Assert.True(Math.Sqrt(dx * dx + dy * dy) <= tolerance,
            $"Corner was ({actual.X:F3},{actual.Y:F3}); expected ({expected.X:F3},{expected.Y:F3}) within {tolerance:F3}px.");
    }
}
