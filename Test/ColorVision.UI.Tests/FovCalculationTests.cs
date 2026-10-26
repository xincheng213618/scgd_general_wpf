using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.FOV2;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        InputOnlyServices services = new();
        LocalFovNode node = new(services);
        CVStartCFC action = new();
        action.MasterValue(null!, 10, (int)ViewResultAlgType.FindLightArea);
        LocalFovInputContext input = node.ResolveInput(action, FovMeasurementMode.CurvedBoundary);
        Assert.Null(input.Corners);
        Assert.Equal(5, input.SourceMasterId);
        Assert.Equal(10, input.LightAreaMasterId);
        Assert.Equal(0, services.CornerReads);
        Assert.Throws<InvalidOperationException>(() => node.ResolveInput(action, FovMeasurementMode.FourCorners));
        Assert.Equal(1, services.CornerReads);
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

    private sealed class InputOnlyServices : ILocalFovNodeServices
    {
        public int CornerReads { get; private set; }
        public AlgResultMasterModel? GetAlgorithmResult(int masterId) => new() { Params = "{\"SourceMasterId\":5}" };
        public MeasureResultImgModel? GetImageResult(int masterId) => new() { Id = masterId };
        public IReadOnlyList<LuminousAreaPoint> GetLightAreaCorners(int masterId)
        {
            CornerReads++;
            throw new InvalidOperationException("Upstream corners are not available.");
        }
        public LocalFlowFrame LoadFrame(string filePath) => throw new NotSupportedException();
        public MeasureResultImgModel? FindImageResult(string? imageFilePath) => throw new NotSupportedException();
        public FovCameraCalibration ResolveCalibration(string? sourceCameraDeviceCode) => throw new NotSupportedException();
        public FovCalculationResult DetectAndCalculate(HImage image, RoiRect roi, double fovDist, double cameraDegrees, IReadOnlyList<LuminousAreaPoint>? coarseCorners, FovMeasurementMode mode) => throw new NotSupportedException();
        public LocalFovPersistenceResult Persist(LocalFovPersistenceRequest request) => throw new NotSupportedException();
        public void Publish(LocalFovPublishRequest request) => throw new NotSupportedException();
    }
}
