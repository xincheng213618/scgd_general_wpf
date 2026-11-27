using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Services.Results.LightArea;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.FOV2;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using ST.Library.UI.NodeEditor;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class FovCalculationTests
{
    private static readonly LuminousAreaPoint[] Corners = [new(40, 30), new(260, 30), new(260, 230), new(40, 230)];

    [Fact]
    public void DefaultModeKeepsUpstreamCornersAndLegacyFieldMeaningsWithoutPixels()
    {
        Assert.Equal(FovMeasurementMode.FourCorners, new FovImageViewOptions().MeasurementMode);
        Assert.Equal(FovMeasurementMode.FourCorners, new LocalFovNode().MeasurementMode);
        Assert.NotNull(typeof(FovCalculator).GetMethod(nameof(FovCalculator.DetectAndCalculate),
            [typeof(HImage), typeof(RoiRect), typeof(double), typeof(double), typeof(double), typeof(double), typeof(IReadOnlyList<LuminousAreaPoint>)]));
        FovCalculationResult result = FovCalculator.DetectAndCalculate(default, default, 9410, 74.2, coarseCorners: Corners);
        Assert.True(result.UsedProvidedCorners);
        Assert.Equal("UpstreamCorners", result.BoundaryMode);
        Assert.Null(result.Measurement.Boundary);
        Assert.Equal(FovCalculator.PixelDistanceToDegrees(220, 9410, 74.2), result.Measurement.DirectionalHorizontalFovDegrees);
        Assert.Equal(FovCalculator.PixelDistanceToDegrees(200, 9410, 74.2), result.Measurement.DirectionalVerticalFovDegrees);
        JObject json = JObject.Parse(LocalFovResultPersistence.BuildLegacyResultJson(result.Measurement));
        Assert.Equal(8, ((JObject)json["result"]!).Count);
        Assert.Equal(result.Measurement.DirectionalHorizontalFovDegrees, json["result"]!["clolorVisionH_Fov"]!.Value<double>());
    }

    [Theory]
    [InlineData(8, 1)]
    [InlineData(16, 3)]
    [InlineData(32, 1)]
    public void CurvedModeRelocatesPixelsAndMeasuresActualCenterEdges(int depth, int channels)
    {
        using SyntheticImage image = new(depth, channels, (x, y) =>
        {
            double nx = (x - 150) / 110d;
            double ny = (y - 130) / 100d;
            return Math.Abs(nx) <= 0.75 + 0.25 * ny * ny && Math.Abs(ny) <= 0.75 + 0.25 * nx * nx;
        });
        byte[] before = image.CopyBytes();
        // Invalid upstream corners must not influence independent curved detection.
        FovCalculationResult result = FovCalculator.DetectAndCalculate(image.Image, default, 9410, 74.2,
            coarseCorners: [new(double.NaN, 0)], mode: FovMeasurementMode.CurvedBoundary);
        FovBoundaryGeometry boundary = Assert.IsType<FovBoundaryGeometry>(result.Measurement.Boundary);
        Assert.Equal("CurvedBoundary", result.BoundaryMode);
        Assert.False(result.UsedProvidedCorners);
        Assert.Equal(before, image.CopyBytes());
        Assert.Empty(result.Measurement.Corners);
        Assert.True(boundary.Left.X > boundary.Contour.Min(point => point.X) + 15);
        Assert.True(boundary.Top.Y > boundary.Contour.Min(point => point.Y) + 15);
        Assert.True(result.Measurement.DirectionalHorizontalFovDegrees < result.Measurement.HorizontalFovDegrees);
        Assert.True(result.Measurement.DirectionalVerticalFovDegrees < result.Measurement.VerticalFovDegrees);
        Assert.Equal((result.Measurement.LeftDownToRightUpDegrees + result.Measurement.LeftUpToRightDownDegrees) / 2, result.Measurement.DiagonalFovDegrees);
        Assert.Equal(FovMeasurementMode.CurvedBoundary, JsonConvert.DeserializeObject<FovImageViewOptions>(
            JsonConvert.SerializeObject(new FovImageViewOptions { MeasurementMode = FovMeasurementMode.CurvedBoundary }))!.MeasurementMode);
    }

    [Theory]
    [InlineData("circle")]
    [InlineData("ellipse")]
    [InlineData("polygon")]
    public void ContourModeMeasuresNonQuadrilateralWithoutInventingCorners(string shape)
    {
        using SyntheticImage image = new(8, 1, (x, y) => shape switch
        {
            "circle" => Math.Pow(x - 150, 2) + Math.Pow(y - 130, 2) <= 80 * 80,
            "ellipse" => Math.Pow((x - 150) / 100d, 2) + Math.Pow((y - 130) / 60d, 2) <= 1,
            _ => Math.Abs(x - 150) <= 95 && Math.Abs(y - 130) <= 70 && Math.Abs(x - 150) + Math.Abs(y - 130) <= 135
        });
        FovMeasurement m = FovCalculator.DetectAndCalculate(image.Image, default, 9410, 74.2, mode: FovMeasurementMode.CurvedBoundary).Measurement;
        Assert.Empty(m.Corners);
        Assert.NotNull(m.Boundary);
        Assert.True(m.DirectionalHorizontalFovDegrees > 0 && m.DirectionalVerticalFovDegrees > 0 && m.DiagonalFovDegrees > 0);
        if (shape == "circle")
        {
            // Its diagonal is a diameter, not the diagonal of an enclosing square.
            Assert.True(m.DiagonalFovDegrees < FovCalculator.PixelDistanceToDegrees(200, 9410, 74.2));
        }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public void CurvedModeSeparatesDimFieldFromThinAnnotationsAndDetachedRing(int channels, bool hasHole)
    {
        using SyntheticImage image = new(8, channels, InField, (x, y, channel) =>
        {
            bool ring = x >= 50 && x <= 250 && y >= 40 && y <= 220
                && (x <= 52 || x >= 248 || y <= 42 || y >= 218);
            double value = InField(x, y) ? 24 + 176 * Math.Sqrt((x - 60) / 180d) : ring ? 120 : 10;
            if (x == 85 && y >= 35 && y <= 225 || y == 100 && x >= 45 && x <= 255)
                value = channels == 1 ? 85 : channel == 2 ? 255 : 0;
            return hasHole && x >= 205 && x <= 220 && y >= 115 && y <= 145 ? 10 : value;
        });
        byte[] before = image.CopyBytes();
        if (hasHole)
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                FovCalculator.DetectAndCalculate(image.Image, default, 9410, 74.2, FovMeasurementMode.CurvedBoundary));
            Assert.Contains("孔洞", error.Message);
        }
        else
        {
            FovCalculationResult result = FovCalculator.DetectAndCalculate(image.Image, default, 9410, 74.2, FovMeasurementMode.CurvedBoundary);
            FovBoundaryGeometry boundary = result.Measurement.Boundary!;
            Assert.True(result.Detection!.Confidence >= 0.25);
            // The sharp field edge lies between these adjacent pixel centres;
            // the brighter detached ring is ten pixels farther out.
            Assert.InRange(boundary.Left.X, 59, 60);
            Assert.InRange(boundary.Right.X, 240, 241);
            Assert.InRange(boundary.Top.Y, 49, 50);
            Assert.InRange(boundary.Bottom.Y, 210, 211);
            Assert.All(boundary.Contour, point =>
            {
                // Across the whole contour, follow the field rather than the
                // detached ring. A spatial median does not promise subpixel
                // accuracy where a stroke crosses the edge.
                Assert.True(DistanceToEdge(point, 60, 50, 240, 210) < DistanceToEdge(point, 50, 40, 250, 220));
            });
        }
        Assert.Equal(before, image.CopyBytes());

        static bool InField(int x, int y) => x >= 60 && x <= 240 && y >= 50 && y <= 210;

        static double DistanceToEdge(LuminousAreaPoint point, double left, double top, double right, double bottom)
        {
            double dx = Math.Max(Math.Max(left - point.X, point.X - right), 0);
            double dy = Math.Max(Math.Max(top - point.Y, point.Y - bottom), 0);
            return dx > 0 || dy > 0 ? Math.Sqrt(dx * dx + dy * dy)
                : Math.Min(Math.Min(point.X - left, right - point.X), Math.Min(point.Y - top, bottom - point.Y));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurvedModeKeepsNoisyBackgroundOutsideTheSeed(bool unevenField)
    {
        using SyntheticImage image = new(32, 1, InField,
            (x, y, _) => !InField(x, y) ? 10 + ((x * 17 + y * 31) % 11 - 5) / 10d
                : !unevenField || x >= 205 && y <= 85 ? 200 : x <= 100 ? 15 : 24);
        FovBoundaryGeometry boundary = FovCalculator.DetectAndCalculate(image.Image, default, 9410, 74.2,
            FovMeasurementMode.CurvedBoundary).Measurement.Boundary!;
        Assert.InRange(boundary.Left.X, 59, 60);
        Assert.InRange(boundary.Right.X, 240, 241);
        Assert.InRange(boundary.Top.Y, 49, 50);
        Assert.InRange(boundary.Bottom.Y, 210, 211);

        static bool InField(int x, int y) => x >= 60 && x <= 240 && y >= 50 && y <= 210;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(double.NaN)]
    public void CurvedModeFollowsOuterGradientPastAnIsolatedInteriorShadow(double unusedMinimumConfidence)
    {
        using SyntheticImage cleanImage = CreateShadowedCircularField(shadowHalfHeight: 0);
        using SyntheticImage shadowedImage = CreateShadowedCircularField(shadowHalfHeight: 2);
        FovBoundaryGeometry clean = FovCalculator.DetectAndCalculate(cleanImage.Image, default, 9410, 74.2,
            FovMeasurementMode.CurvedBoundary).Measurement.Boundary!;
        FovCalculationResult result = FovCalculator.DetectAndCalculate(shadowedImage.Image, default, 9410, 74.2,
            FovMeasurementMode.CurvedBoundary);
        FovBoundaryGeometry measured = result.Measurement.Boundary!;
        Assert.InRange(result.Detection!.Confidence!.Value, 0.25, 1);
        Assert.InRange(measured.Right.X, clean.Right.X - 2, clean.Right.X + 2);
        LuminousAreaPoint[] nearRight = measured.Contour.Where(point => point.X > clean.Center.X && Math.Abs(point.Y - clean.Center.Y) < 4).ToArray();
        Assert.Contains(nearRight, point => Math.Abs(point.Y - clean.Center.Y) < 1);
        Assert.All(nearRight, point => Assert.InRange(point.X, clean.Right.X - 2, clean.Right.X + 2));

        FovCalculationResult compatibility = FovCalculator.DetectAndCalculate(shadowedImage.Image, default, 9410, 74.2,
            FovMeasurementMode.CurvedBoundary, minimumConfidence: unusedMinimumConfidence);
        Assert.Equal(result.Detection.Confidence, compatibility.Detection!.Confidence);
        Assert.Equal(measured.Right, compatibility.Measurement.Boundary!.Right);
        Assert.Equal(measured.Contour, compatibility.Measurement.Boundary.Contour);
    }

    [Fact]
    public void CurvedModeFollowsOuterGradientPastAWideInteriorShadow()
    {
        // The wider attenuation affects adjacent measured rays on both sides of 0°.
        using SyntheticImage cleanImage = CreateShadowedCircularField(shadowHalfHeight: 0);
        using SyntheticImage shadowedImage = CreateShadowedCircularField(shadowHalfHeight: 20);
        FovBoundaryGeometry clean = FovCalculator.DetectAndCalculate(cleanImage.Image, default, 9410, 74.2,
            FovMeasurementMode.CurvedBoundary).Measurement.Boundary!;
        FovBoundaryGeometry measured = FovCalculator.DetectAndCalculate(shadowedImage.Image, default, 9410, 74.2,
            FovMeasurementMode.CurvedBoundary).Measurement.Boundary!;

        // The shadow's sharp inner step is well inside the actual outer gradient.
        Assert.InRange(measured.Right.X, clean.Right.X - 2, clean.Right.X + 2);
        Assert.All(measured.Contour.Where(point => point.X > clean.Center.X && Math.Abs(point.Y - clean.Center.Y) < 24),
            point => Assert.InRange(point.X, clean.Right.X - 2, clean.Right.X + 2));
    }

    [Fact]
    public void CurvedModeReportsLowContrastAndMeasuresTheVisibleEdge()
    {
        const int centerX = 328, centerY = 324, edgeRadius = 240;
        using SyntheticImage image = new(32, 1, (_, _) => true, (x, y, _) =>
        {
            int dx = x - centerX;
            int dy = y - centerY;
            double radius = Math.Sqrt(dx * dx + dy * dy);
            double blend = Math.Exp(-Math.Pow(Math.Atan2(dy, dx) / 0.34, 4));
            // The slowly fading glare outside the right edge reduces the measured
            // contrast, but the light-to-dark step at the circle is still visible.
            return radius <= edgeRadius
                ? 180 - (180 - 42) * blend * Math.Clamp((radius - 75) / 130, 0, 1)
                : 42 * 0.88 * blend * Math.Clamp(1 - (radius - edgeRadius) / 55, 0, 1);
        }, width: 656, height: 648);

        FovCalculationResult result = FovCalculator.DetectAndCalculate(image.Image, default, 9410, 74.2, FovMeasurementMode.CurvedBoundary);
        Assert.InRange(result.Detection!.Confidence!.Value, 0.1, 0.24);
        FovBoundaryGeometry boundary = result.Measurement.Boundary!;
        Assert.InRange(boundary.Right.X, centerX + edgeRadius - 2, centerX + edgeRadius + 1);
        Assert.All(boundary.Contour.Where(point => point.X > centerX && Math.Abs(point.Y - centerY) < 40),
            point => Assert.InRange(point.X, centerX + edgeRadius - 5, centerX + edgeRadius + 1));
    }

    [Fact]
    public void CurvedModeStillRejectsAnArcWithoutALocatableEdge()
    {
        using SyntheticImage image = new(32, 1, (_, _) => true, (x, y, _) =>
        {
            int dx = x - 328;
            int dy = y - 324;
            double radius = Math.Sqrt(dx * dx + dy * dy);
            double direction = Math.Atan2(dy, dx);
            // The field fades gradually across a right-hand sector without a
            // resolved local edge, while the rest of the boundary is visible.
            double fadeWidth = 90 + 155 * Math.Exp(-Math.Pow(direction / 0.16, 4));
            return 180 * Math.Clamp((300 - radius) / fadeWidth, 0, 1);
        }, width: 656, height: 648);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => FovCalculator.DetectAndCalculate(
            image.Image, default, 9410, 74.2, FovMeasurementMode.CurvedBoundary, minimumConfidence: 0));
        Assert.Contains("缺失、裁切或低对比边界", error.Message);
    }

    [Theory]
    [InlineData(false, "曲边模式要求连续填充图案，当前轮廓含孔洞或同一方向的多段边界。")]
    [InlineData(true, "曲边发光区存在缺失、裁切或低对比边界。")]
    public void CurvedModeLargeSixteenBitImageReportsTheFirstRayFailure(bool fadeRightEdge, string expectedMessage)
    {
        using SyntheticImage image = new(16, 1, (_, _) => true, (x, y, _) =>
        {
            int dx = x - 328;
            int dy = y - 324;
            // An open notch causes a later ray to re-enter the same component.
            if (dx <= 30 && Math.Abs(dy - 155) <= 8) return 0;
            double radius = Math.Sqrt(dx * dx + dy * dy);
            double direction = Math.Atan2(dy, dx);
            double fadeWidth = fadeRightEdge ? 90 + 155 * Math.Exp(-Math.Pow(direction / 0.16, 4)) : 90;
            return 36000 * Math.Clamp((300 - radius) / fadeWidth, 0, 1);
        }, width: 656, height: 648);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => FovCalculator.DetectAndCalculate(
            image.Image, default, 9410, 74.2, FovMeasurementMode.CurvedBoundary));
        Assert.Equal(expectedMessage, error.Message);
    }

    [Fact]
    public void CurvedModeConfidenceIgnoresAnIsolatedWeakRayAtTheContourSeam()
    {
        double? confidence = FovCurvedBoundaryDetector.CalculateContourConfidence([0.19, 0.83, 0.92, 0.72]);
        Assert.Equal<double?>(0.72, confidence);
    }

    [Fact]
    public void CurvedModeConfidenceRetainsAdjacentWeakRaysAcrossTheContourSeam()
    {
        double? confidence = FovCurvedBoundaryDetector.CalculateContourConfidence([0.19, 0.83, 0.92, 0.21]);
        Assert.Equal<double?>(0.21, confidence);
    }

    [Fact]
    public void CurvedModeConfidenceDoesNotBridgeAFrameEdgeClosure()
    {
        double? confidence = FovCurvedBoundaryDetector.CalculateContourConfidence([0.19, null, 0.92, 0.72]);
        Assert.Equal<double?>(0.19, confidence);
    }

    [Fact]
    public void CurvedModeConfidenceRequiresAMeasuredRay()
    {
        Assert.Null(FovCurvedBoundaryDetector.CalculateContourConfidence([null, null, null, null]));
    }

    [Fact]
    public void CurvedModeRejectsResolvableHoleBetweenContourRays()
    {
        double angle = 20.25 * Math.PI / 180;
        double holeX = 500 + 360 * Math.Cos(angle);
        double holeY = 420 + 360 * Math.Sin(angle);
        using SyntheticImage image = new(8, 1, (x, y) => x >= 90 && x <= 910 && y >= 80 && y <= 760
            && !(Math.Abs(x - holeX) <= 2 && Math.Abs(y - holeY) <= 2), width: 1000, height: 840);
        // The spatial median leaves a 2x2 dark seed island, which fits between
        // the fixed angular rays. Topology must be checked across the seed grid.
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            FovCalculator.DetectAndCalculate(image.Image, default, 9410, 74.2, FovMeasurementMode.CurvedBoundary));
        Assert.Contains("孔洞", error.Message);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 1)]
    [InlineData(1, 2)]
    [InlineData(1, 3)]
    [InlineData(1, 4)]
    public void CurvedModeUsesAvailableBackgroundNearRoiEdge(int margin, int side)
    {
        int left = side == 1 ? margin : 60;
        int right = side == 2 ? 299 - margin : 240;
        int top = side == 3 ? margin : 50;
        int bottom = side == 4 ? 259 - margin : 210;
        using SyntheticImage image = new(8, 1, (x, y) => x >= left && x <= right && y >= top && y <= bottom);
        FovBoundaryGeometry boundary = FovCalculator.DetectAndCalculate(image.Image, default, 9410, 74.2,
            FovMeasurementMode.CurvedBoundary).Measurement.Boundary!;
        Assert.InRange(boundary.Left.X, left - 1, left);
        Assert.InRange(boundary.Right.X, right, right + 1);
        Assert.InRange(boundary.Top.Y, top - 1, top);
        Assert.InRange(boundary.Bottom.Y, bottom, bottom + 1);
    }

    [Fact]
    public void ContourFlowInputUsesSourceImageAndNeverLoadsUpstreamCorners()
    {
        FovTestServices services = new();
        LocalFovNode node = new(services);
        CVStartCFC action = new();
        var endpoint = new MySqlConnectedEndpoint(_ => true, () => throw new NotSupportedException());
        action.MasterValue(null!, 10, (int)ViewResultAlgType.FindLightArea);
        LocalFovInputContext input = node.ResolveInput(action, FovMeasurementMode.CurvedBoundary, endpoint);
        Assert.Null(input.Corners);
        Assert.Equal(5, input.SourceMasterId);
        Assert.Equal(10, input.LightAreaMasterId);
        Assert.Equal(0, services.CornerReads);
        Assert.Throws<InvalidOperationException>(() => node.ResolveInput(action, FovMeasurementMode.FourCorners, endpoint));
        Assert.Equal(1, services.CornerReads);
    }

    [Fact]
    public void FlowMinimumConfidenceRoundTripsAndReachesCalculation()
    {
        const double configuredConfidence = 0.72;
        LocalFovNode original = new();
        original.Create();
        Assert.Equal(0.25, original.MinimumConfidence);
        Assert.False(TypeDescriptor.GetProperties(original)[nameof(LocalFovNode.MinimumConfidence)]!.IsBrowsable);
        original.MinimumConfidence = -1;
        Assert.Equal(0, original.MinimumConfidence);
        original.MinimumConfidence = 2;
        Assert.Equal(1, original.MinimumConfidence);
        original.MinimumConfidence = double.NaN;
        Assert.Equal(0.25, original.MinimumConfidence);
        original.MinimumConfidence = configuredConfidence;

        STNodeSavedState state = STNodeSavedState.Parse(original.GetSaveData());
        FovTestServices services = new(Corners);
        LocalFovNode restored = new(services);
        restored.Create();
        restored.OnLoadNode(state.Properties);
        Assert.Equal(configuredConfidence, restored.MinimumConfidence);

        CVStartCFC action = new("fov-minimum-confidence");
        using var resources = action.RuntimeResources;
        MySqlConnectedEndpoint endpoint = new(_ => true, () => throw new NotSupportedException());
        new FlowMeasurementBatch(action.SerialNumber, 1, string.Empty, Stopwatch.GetTimestamp(), endpoint).Bind(resources);
        action.MasterValue(null!, 10, (int)ViewResultAlgType.FindLightArea);
        Assert.True(restored.ExecuteSynchronously(action).Calculation.UsedProvidedCorners);
        Assert.Equal<double?>(configuredConfidence, services.ReceivedMinimumConfidence);
    }

    [Theory]
    [InlineData("source ID")]
    [InlineData("legacy path")]
    [InlineData("calculation failure")]
    [InlineData("detail failure")]
    public void BoundFovReadsAndPersistsOnItsOriginalDatabase(string scenario)
    {
        string folder = Directory.CreateTempSubdirectory("cv-fov-database-").FullName;
        string pathA = Path.Combine(folder, "a.db"), pathB = Path.Combine(folder, "b.db");
        string imageA = Path.Combine(folder, "a-image.png"), imageB = Path.Combine(folder, "b-image.png");
        string legacyPath = Path.Combine(folder, "legacy-image.raw");
        string resultDirectory = Path.Combine(folder, "results");
        MySqlConnectedEndpoint? active = null;
        MySqlConnectedEndpoint a = new(endpoint => ReferenceEquals(active, endpoint), () => Open(pathA));
        MySqlConnectedEndpoint b = new(endpoint => ReferenceEquals(active, endpoint), () => Open(pathB));
        var action = new CVStartCFC("shared-fov-batch");
        using var resources = action.RuntimeResources;
        try
        {
            File.WriteAllBytes(imageA, []);
            File.WriteAllBytes(imageB, []);
            active = a;
            var originalCorners = scenario == "calculation failure" ? Corners.Reverse().ToArray() : Corners;
            var differentCorners = Corners.Select(point => new LuminousAreaPoint(point.X + 100, point.Y + 200)).ToArray();
            var sourceA = Seed(a, imageA, originalCorners, scenario == "legacy path", isBound: true);
            var sourceB = Seed(b, imageB, differentCorners, lookupByPath: false, isBound: false);
            Assert.Equal(sourceA, sourceB);
            action.MasterValue(null!, sourceA.AreaId, (int)ViewResultAlgType.FindLightArea);
            active = b;
            Assert.False(a.IsCurrent);

            const double configuredConfidence = 0.61;
            var node = new LocalFovNode(new LocalFovNodeServices(resultDirectory)) { MinimumConfidence = configuredConfidence };
            ResultMessage? received = null;
            using IDisposable subscription = ResultMessageBus.Default.Subscribe(message =>
            {
                if (message.SerialNumber == action.SerialNumber) received = message;
            });
            if (scenario == "detail failure")
            {
                using (var db = a.CreateDbClient())
                    db.Ado.ExecuteCommand("CREATE TRIGGER reject_fov_detail BEFORE INSERT ON t_scgd_algorithm_result_detail_common "
                        + "BEGIN SELECT RAISE(ABORT, 'rejected FOV detail in fixture'); END;");
                Exception error = Assert.ThrowsAny<Exception>(() => node.ExecuteSynchronously(action));
                Assert.Contains("rejected FOV detail in fixture", error.ToString());
                Assert.Null(received);
                Assert.Empty(Directory.EnumerateFiles(resultDirectory));
            }
            else if (scenario == "calculation failure")
            {
                InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => node.ExecuteSynchronously(action));
                Assert.IsType<ArgumentException>(error.InnerException);
                Assert.False(Directory.Exists(resultDirectory));
            }
            else
            {
                LocalFovNodeResultData result = node.ExecuteSynchronously(action);
                Assert.Equal(sourceA.ImageId, result.SourceMasterId);
                Assert.Equal(sourceA.AreaId, result.LightAreaMasterId);
                Assert.Equal(imageA, result.ImageFilePath);
                Assert.Equal(Corners, result.Calculation.Measurement.Corners);
                Assert.True(File.Exists(result.ResultFilePath));
            }

            using var dbA = a.CreateDbClient();
            using var dbB = b.CreateDbClient();
            Assert.Single(dbB.Queryable<AlgResultMasterModel>().ToList());
            Assert.Empty(dbB.Queryable<DetailCommonModel>().ToList());
            var masters = dbA.Queryable<AlgResultMasterModel>().Where(row => row.ImgFileType == ViewResultAlgType.FOV).ToList();
            if (scenario == "detail failure")
            {
                Assert.Empty(masters);
                Assert.Empty(dbA.Queryable<DetailCommonModel>().ToList());
                return;
            }

            var master = Assert.Single(masters);
            Assert.Equal(sourceA.BatchId, master.BatchId);
            Assert.Equal(imageA, master.ImgFile);
            JObject parameters = JObject.Parse(master.Params!);
            Assert.Equal(sourceA.ImageId, parameters.Value<int>("SourceMasterId"));
            Assert.Equal(configuredConfidence, parameters.Value<double>("MinimumConfidence"));
            Assert.NotNull(received);
            Assert.Same(a, received.DatabaseEndpoint);
            Assert.Equal(master.Id, received.Data.MasterId);
            Assert.Equal((int)ViewResultAlgType.FOV, received.Data.MasterResultType);
            if (scenario == "calculation failure")
            {
                Assert.Equal(LocalFovNode.CalculationFailureResultCode, master.ResultCode);
                Assert.Contains("FOV coarse corners are invalid", master.Result);
                Assert.Equal(master.Result, parameters.Value<string>("FailureReason"));
                Assert.Empty(dbA.Queryable<DetailCommonModel>().ToList());
            }
            else
            {
                Assert.Equal(0, master.ResultCode);
                var detail = Assert.Single(dbA.Queryable<DetailCommonModel>().ToList());
                Assert.Equal(master.Id, detail.PId);
                string resultFile = JObject.Parse(detail.ResultJson).Value<string>("ResultFileName")!;
                Assert.Equal(Assert.Single(Directory.EnumerateFiles(resultDirectory)), resultFile);
                Assert.Equal("Success", JObject.Parse(File.ReadAllText(resultFile))["result"]?.Value<string>("message"));
            }
        }
        finally
        {
            if (Directory.Exists(resultDirectory))
            {
                foreach (string resultFile in Directory.EnumerateFiles(resultDirectory))
                    File.Delete(resultFile);
                Directory.Delete(resultDirectory);
            }
            File.Delete(imageA);
            File.Delete(imageB);
            File.Delete(pathA);
            File.Delete(pathB);
            Directory.Delete(folder);
        }

        (int BatchId, int ImageId, int AreaId) Seed(MySqlConnectedEndpoint endpoint, string imageFile,
            LuminousAreaPoint[] corners, bool lookupByPath, bool isBound)
        {
            using var db = endpoint.CreateDbClient();
            db.CodeFirst.InitTables(typeof(MeasureBatchModel), typeof(MeasureResultImgModel), typeof(AlgResultMasterModel),
                typeof(AlgResultLightAreaModel), typeof(DetailCommonModel));
            var batch = new MeasureBatchModel { Name = action.SerialNumber, Code = action.SerialNumber };
            var inserted = FlowMeasurementBatch.Insert(batch, endpoint);
            if (isBound) inserted.Bind(resources);
            int imageId = db.Insertable(new MeasureResultImgModel
            {
                BatchId = batch.Id, FileUrl = imageFile, RawFile = Path.GetFileName(legacyPath), CreateDate = DateTime.Now
            }).ExecuteReturnIdentity();
            int areaId = db.Insertable(new AlgResultMasterModel
            {
                BatchId = batch.Id, ImgFileType = ViewResultAlgType.FindLightArea, ImgFile = legacyPath, CreateDate = DateTime.Now,
                Params = lookupByPath ? null : JsonConvert.SerializeObject(new { SourceMasterId = isBound ? imageId : imageId + 1000 })
            }).ExecuteReturnIdentity();
            db.Insertable(corners.Select(point => new AlgResultLightAreaModel
            {
                Pid = areaId, PosX = (float)point.X, PosY = (float)point.Y
            }).ToList()).ExecuteCommand();
            return (batch.Id, imageId, areaId);
        }

        static SqlSugarClient Open(string path) => new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={path};Pooling=False", DbType = DbType.Sqlite,
            IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute
        });
    }

    [Fact]
    public void CurvedModeRequiresPixelsEvenWhenFourCornersExist()
    {
        Assert.Throws<ArgumentException>(() => FovCalculator.DetectAndCalculate(default, default, 9410, 74.2,
            coarseCorners: Corners, mode: FovMeasurementMode.CurvedBoundary));
    }

    [Fact]
    public void CurvedModeRestoresRoiCoordinatesAndRejectsCroppedBoundary()
    {
        using SyntheticImage image = new(8, 1, (x, y) => x >= 60 && x <= 240 && y >= 50 && y <= 210);
        FovBoundaryGeometry full = FovCalculator.DetectAndCalculate(image.Image, default, 9410, 74.2,
            mode: FovMeasurementMode.CurvedBoundary).Measurement.Boundary!;
        FovBoundaryGeometry roi = FovCalculator.DetectAndCalculate(image.Image, new RoiRect(20, 10, 260, 240), 9410, 74.2,
            mode: FovMeasurementMode.CurvedBoundary).Measurement.Boundary!;
        Assert.Equal(full.Left, roi.Left);
        Assert.Equal(full.Right, roi.Right);
        Assert.Equal(full.Top, roi.Top);
        Assert.Equal(full.Bottom, roi.Bottom);
        Assert.Throws<InvalidOperationException>(() => FovCalculator.DetectAndCalculate(image.Image, new RoiRect(80, 10, 200, 240), 9410, 74.2,
            mode: FovMeasurementMode.CurvedBoundary));
    }

    [Fact]
    public void SeedMedianMatchesSortedWindowsIncludingSimdTails()
    {
        // Cover every tail length on the SIMD width of the current machine.
        foreach (int width in Enumerable.Range(0, 2 * Vector<double>.Count).Concat([128, 1021]))
            AssertSeedMedianMatchesSortedWindows(width);
    }

    private static void AssertSeedMedianMatchesSortedWindows(int width)
    {
        var random = new Random(16081);
        double[] levels = [double.MinValue, -1, 0, 0, 0, 255, 32768, 65535, 65535, 196605, double.MaxValue];
        double[][] rows = new double[5][];
        for (int y = 0; y < rows.Length; y++)
        {
            rows[y] = new double[width + 4];
            for (int x = 0; x < rows[y].Length; x++)
                rows[y][x] = levels[random.Next(levels.Length)];
            if (width >= 9)
            {
                rows[y].AsSpan(0, 5).Fill(double.MinValue);
                rows[y].AsSpan(width - 1, 5).Fill(double.MaxValue);
            }
        }

        double[] output = new double[width];
        Array.Fill(output, double.NaN);
        bool written = FovSeedMedian.TryWriteRow(rows[0], rows[1], rows[2], rows[3], rows[4], output);
        Assert.Equal(Vector.IsHardwareAccelerated && width >= Vector<double>.Count, written);
        if (!written)
        {
            Assert.All(output, value => Assert.True(double.IsNaN(value)));
            return;
        }

        double[] window = new double[25];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < rows.Length; y++)
                rows[y].AsSpan(x, 5).CopyTo(window.AsSpan(y * 5, 5));
            Array.Sort(window);
            Assert.Equal(window[12], output[x]);
        }
    }

    [Fact]
    public void CurvedModeLargeSixteenBitRoiMatchesTheSerialFloatPath()
    {
        // The seed step is two, so both ROI edges need the clamped halo.
        // The four-channel image uses the same intensity as the grayscale
        // reference while varying alpha and using padded rows.
        RoiRect roi = new(7, 11, 1008, 1028);
        using SyntheticImage color = new(16, 4, InField, (x, y, channel) =>
            channel == 3 ? (x * 71 + y * 113) % 65536 : Intensity(x, y) + (channel - 1) * 2000,
            width: 1020, height: 1044);
        using SyntheticImage grayscale = new(32, 1, InField, (x, y, _) => Intensity(x, y), width: 1020, height: 1044);

        FovCalculationResult reference = FovCalculator.DetectAndCalculate(grayscale.Image, roi, 9410, 74.2, FovMeasurementMode.CurvedBoundary);
        FovCalculationResult result = FovCalculator.DetectAndCalculate(color.Image, roi, 9410, 74.2, FovMeasurementMode.CurvedBoundary);

        FovBoundaryGeometry expected = reference.Measurement.Boundary!;
        FovBoundaryGeometry actual = result.Measurement.Boundary!;
        Assert.Equal(expected with { DiagonalPoints = actual.DiagonalPoints, Contour = actual.Contour }, actual);
        Assert.Equal(expected.DiagonalPoints, actual.DiagonalPoints);
        Assert.Equal(expected.Contour, actual.Contour);
        Assert.Equal(reference.Measurement.DiagonalFovDegrees, result.Measurement.DiagonalFovDegrees);
        Assert.Equal(reference.Detection!.Confidence, result.Detection!.Confidence);

        static bool InField(int x, int y) => Math.Pow((x - 510) / 406d, 2) + Math.Pow((y - 525) / 396d, 2) <= 1;
        static double Intensity(int x, int y) => (InField(x, y) ? 36000 : 3000) + (x * 17 + y * 31) % 127;
    }

    [Theory]
    [InlineData("Bottom", false)]
    [InlineData("Bottom", true)]
    [InlineData("Top", true)]
    [InlineData("Left", true)]
    [InlineData("Right", true)]
    public void CurvedModeUsesPhysicalImageEdgeOnlyWhereTheFieldReachesIt(string side, bool hasCenterNotch)
    {
        const int longDimension = 1201, shortDimension = 640;
        const int centerX = 600, centerY = 356, radius = 312, notchY = 620;
        bool transposed = side is "Left" or "Right";
        int width = transposed ? shortDimension : longDimension;
        int height = transposed ? longDimension : shortDimension;
        using SyntheticImage image = new(8, 1, (x, y) =>
        {
            LuminousAreaPoint point = ToBottomFacingCoordinates(new(x, y));
            double dx = point.X - centerX;
            double dy = point.Y - centerY;
            return dx * dx + dy * dy <= radius * radius
                && (!hasCenterNotch || point.Y <= notchY + dx * dx / 200d);
        }, width: width, height: height);

        foreach (RoiRect searchArea in new[] { default(RoiRect), SearchArea(0) })
        {
            FovBoundaryGeometry boundary = FovCalculator.DetectAndCalculate(image.Image, searchArea, 9410, 74.2,
                FovMeasurementMode.CurvedBoundary).Measurement.Boundary!;
            LuminousAreaPoint[] contour = boundary.Contour.Select(ToBottomFacingCoordinates).ToArray();
            Assert.Equal(shortDimension - 1, contour.Max(point => point.Y));
            Assert.Contains(contour, point => point.Y == shortDimension - 1 && point.X < centerX - 75);
            Assert.Contains(contour, point => point.Y == shortDimension - 1 && point.X > centerX + 75);
            LuminousAreaPoint endpoint = side switch
            {
                "Top" => boundary.Top,
                "Left" => boundary.Left,
                "Right" => boundary.Right,
                _ => boundary.Bottom
            };
            double measuredY = ToBottomFacingCoordinates(endpoint).Y;
            if (hasCenterNotch)
                Assert.InRange(measuredY, notchY - 1, notchY + 2);
            else
                Assert.Equal(shortDimension - 1, measuredY);
        }

        Assert.Throws<InvalidOperationException>(() => FovCalculator.DetectAndCalculate(image.Image, SearchArea(20),
            9410, 74.2, FovMeasurementMode.CurvedBoundary));

        LuminousAreaPoint ToBottomFacingCoordinates(LuminousAreaPoint point) => side switch
        {
            "Top" => new(point.X, shortDimension - 1 - point.Y),
            "Left" => new(point.Y, shortDimension - 1 - point.X),
            "Right" => new(point.Y, point.X),
            _ => point
        };

        RoiRect SearchArea(int insetFromPhysicalEdge) => side switch
        {
            "Top" => new(150, insetFromPhysicalEdge, 901, shortDimension - 20 - insetFromPhysicalEdge),
            "Left" => new(insetFromPhysicalEdge, 150, shortDimension - 20 - insetFromPhysicalEdge, 901),
            "Right" => new(20, 150, shortDimension - 20 - insetFromPhysicalEdge, 901),
            _ => new(150, 20, 901, shortDimension - 20 - insetFromPhysicalEdge)
        };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurvedModeDistinguishesCornerBackgroundFromHolesWhenEveryImageEdgeIsClipped(bool hasHole)
    {
        using SyntheticImage image = new(8, 1, (x, y) => (x - 150) * (x - 150) + (y - 130) * (y - 130) <= 168 * 168
            && !(hasHole && Math.Abs(x - 210) <= 6 && Math.Abs(y - 140) <= 6));
        if (hasHole)
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                FovCalculator.DetectAndCalculate(image.Image, default, 9410, 74.2, FovMeasurementMode.CurvedBoundary));
            Assert.Contains("孔洞", error.Message);
            return;
        }

        FovBoundaryGeometry boundary = FovCalculator.DetectAndCalculate(image.Image, default, 9410, 74.2,
            FovMeasurementMode.CurvedBoundary).Measurement.Boundary!;
        Assert.Equal(0, boundary.Left.X);
        Assert.Equal(image.Image.cols - 1, boundary.Right.X);
        Assert.Equal(0, boundary.Top.Y);
        Assert.Equal(image.Image.rows - 1, boundary.Bottom.Y);
    }

    [Fact]
    public void CurvedModeRejectsBlankAndUnfilledCenterInsteadOfReturningZeroFov()
    {
        using SyntheticImage blank = new(8, 1, (_, _) => false);
        using SyntheticImage ring = new(8, 1, (x, y) => x >= 40 && x <= 260 && y >= 30 && y <= 230
            && (x < 70 || x > 230 || y < 60 || y > 200));
        using SyntheticImage hole = new(8, 1, (x, y) => x >= 40 && x <= 260 && y >= 30 && y <= 230
            && !(x >= 205 && x <= 220 && y >= 115 && y <= 145));
        Assert.Throws<InvalidOperationException>(() => FovCalculator.DetectAndCalculate(blank.Image, default, 9410, 74.2, mode: FovMeasurementMode.CurvedBoundary));
        Assert.Throws<InvalidOperationException>(() => FovCalculator.DetectAndCalculate(ring.Image, default, 9410, 74.2, mode: FovMeasurementMode.CurvedBoundary));
        Assert.Throws<InvalidOperationException>(() => FovCalculator.DetectAndCalculate(hole.Image, default, 9410, 74.2, mode: FovMeasurementMode.CurvedBoundary));
        Assert.Throws<ArgumentOutOfRangeException>(() => FovCalculator.DetectAndCalculate(default, default, 9410, 74.2, mode: (FovMeasurementMode)99));
    }

    private static SyntheticImage CreateShadowedCircularField(int shadowHalfHeight) => new(32, 1, (_, _) => true, (x, y, _) =>
    {
        int dx = x - 328;
        int dy = y - 324;
        double radius = Math.Sqrt(dx * dx + dy * dy);
        double falloff = Math.Clamp((300 - radius) * 2, 0, 180);
        // A sharp brightness drop inside the broad outer transition.
        double shadow = shadowHalfHeight > 0 && dx > 0 && Math.Abs(dy) <= shadowHalfHeight && radius >= 250
            ? 8 * Math.Clamp((266 - radius) / 12, 0, 1) : 0;
        return Math.Max(0, falloff - shadow);
    }, width: 656, height: 648);

    private sealed class SyntheticImage : IDisposable
    {
        public HImage Image;
        private readonly int length;

        public SyntheticImage(int depth, int channels, Func<int, int, bool> inside, Func<int, int, int, double>? pixelValue = null, int width = 300, int height = 260)
        {
            int stride = width * channels * (depth / 8) + 16;
            length = stride * height;
            byte[] pixels = new byte[length];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    for (int c = 0; c < channels; c++)
                    {
                        int offset = y * stride + (x * channels + c) * (depth / 8);
                        bool bright = inside(x, y);
                        double value = pixelValue?.Invoke(x, y, c) ?? (depth == 16 ? bright ? 40000 : 1000 : bright ? 200 : 10);
                        if (depth == 8) pixels[offset] = (byte)value;
                        else if (depth == 16) BitConverter.GetBytes((ushort)value).CopyTo(pixels, offset);
                        else BitConverter.GetBytes((float)value).CopyTo(pixels, offset);
                    }
            Image = new HImage { rows = height, cols = width, channels = channels, depth = depth, stride = stride, pData = Marshal.AllocCoTaskMem(length) };
            Marshal.Copy(pixels, 0, Image.pData, length);
        }

        public byte[] CopyBytes()
        {
            byte[] pixels = new byte[length];
            Marshal.Copy(Image.pData, pixels, 0, length);
            return pixels;
        }

        public void Dispose() => Image.Dispose();
    }

    private sealed class FovTestServices(IReadOnlyList<LuminousAreaPoint>? corners = null) : ILocalFovNodeServices
    {
        public int CornerReads { get; private set; }
        public double? ReceivedMinimumConfidence { get; private set; }
        public AlgResultMasterModel? GetAlgorithmResult(MySqlConnectedEndpoint endpoint, int masterId) => new() { Params = "{\"SourceMasterId\":5}" };
        public MeasureResultImgModel? GetImageResult(MySqlConnectedEndpoint endpoint, int masterId) => new() { Id = masterId };
        public IReadOnlyList<LuminousAreaPoint> GetLightAreaCorners(MySqlConnectedEndpoint endpoint, int masterId)
        {
            CornerReads++;
            return corners ?? throw new InvalidOperationException("Upstream corners are not available.");
        }
        public LocalFlowFrame LoadFrame(string filePath) => throw new NotSupportedException();
        public MeasureResultImgModel? FindImageResult(MySqlConnectedEndpoint endpoint, string? imageFilePath) => throw new NotSupportedException();
        public FovCameraCalibration ResolveCalibration(string? sourceCameraDeviceCode, MySqlConnectedEndpoint endpoint) => new() { CameraDeviceCode = string.Empty };
        public FovCalculationResult DetectAndCalculate(HImage image, RoiRect roi, double fovDist, double cameraDegrees,
            IReadOnlyList<LuminousAreaPoint>? coarseCorners, FovMeasurementMode mode, double minimumConfidence)
        {
            ReceivedMinimumConfidence = minimumConfidence;
            return LocalFovNodeServices.Instance.DetectAndCalculate(image, roi, fovDist, cameraDegrees, coarseCorners, mode, minimumConfidence);
        }
        public LocalFovPersistenceResult Persist(LocalFovPersistenceRequest request) => new() { MasterId = 1, ResultFilePath = "fixture.json" };
        public void Publish(LocalFovPublishRequest request) { }
    }
}
