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

        public SyntheticImage(int depth, int channels, Func<int, int, bool> inside)
        {
            int stride = 300 * channels * (depth / 8) + 16;
            length = stride * 260;
            byte[] pixels = new byte[length];
            for (int y = 0; y < 260; y++)
                for (int x = 0; x < 300; x++)
                    for (int c = 0; c < channels; c++)
                    {
                        int offset = y * stride + (x * channels + c) * (depth / 8);
                        bool bright = inside(x, y);
                        if (depth == 8) pixels[offset] = bright ? (byte)200 : (byte)10;
                        else if (depth == 16) BitConverter.GetBytes(bright ? (ushort)40000 : (ushort)1000).CopyTo(pixels, offset);
                        else BitConverter.GetBytes(bright ? 200f : 10f).CopyTo(pixels, offset);
                    }
            Image = new HImage { rows = 260, cols = 300, channels = channels, depth = depth, stride = stride, pData = Marshal.AllocCoTaskMem(length) };
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
