using ColorVision.Engine.Services.PhyCameras.Calibration.Creation;
using cvColorVision;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CalibrationColorCreationTests
{
    private static readonly double[][] Inputs = { new[] { 10d, 20, 30 }, new[] { 30d, 10, 20 }, new[] { 20d, 30, 10 }, new[] { 40d, 25, 35 } };

    [Theory]
    [InlineData(CalibrationType.LumFourColor)]
    [InlineData(CalibrationType.LumMultiColor)]
    public void MatrixFitRecoversKnownTransformIncludingNegativeCoefficients(CalibrationType type)
    {
        double[] matrix = { 2, -0.1, 0.2, 0.1, 3, 0.2, 0.2, 0.1, 4 };
        var samples = Inputs.Select(raw => Sample(raw,
            matrix[0] * raw[0] + matrix[1] * raw[1] + matrix[2] * raw[2],
            matrix[3] * raw[0] + matrix[4] * raw[1] + matrix[5] * raw[2],
            matrix[6] * raw[0] + matrix[7] * raw[1] + matrix[8] * raw[2])).ToArray();
        var fit = CalibrationColorCreation.Fit(samples, type, 16);
        for (int i = 0; i < 9; i++) Assert.Equal(matrix[i], type == CalibrationType.LumMultiColor
            ? fit.Json["pa"]![i]!.Value<double>() : fit.Json[((char)('a' + i)).ToString()]!.Value<double>(), 10);
        Assert.True(fit.RelativeRmsError < 1e-12);
    }

    [Fact]
    public void OneColorAndLuminanceFitHonorNativeRestrictedEquations()
    {
        var samples = Inputs.Select(raw => Sample(raw, 2 * raw[0] - 0.1 * raw[2], 3 * raw[1], 4 * raw[2])).ToArray();
        var fit = CalibrationColorCreation.Fit(samples, CalibrationType.LumOneColor, 8);
        Assert.Equal(2, fit.Json["a"]!.Value<double>(), 10);
        Assert.Equal(3, fit.Json["b"]!.Value<double>(), 10);
        Assert.Equal(4, fit.Json["c"]!.Value<double>(), 10);
        Assert.Equal(-0.1, fit.Json["d"]!.Value<double>(), 10);
        var mono = CalibrationColorCreation.Fit(new[] { Sample(new[] { 10d }, 10, 25, 10), Sample(new[] { 20d }, 20, 50, 20) }, CalibrationType.Luminance, 16);
        Assert.Equal(2.5, mono.Json["a"]!.Value<double>(), 10);
    }

    [Fact]
    public void RankDeficiencyAndInvalidReferencesDoNotProduceDefaultCoefficients()
    {
        var repeated = Enumerable.Range(0, 4).Select(_ => Sample(new[] { 10d, 20, 30 }, 20, 30, 40)).ToArray();
        Assert.Throws<InvalidDataException>(() => CalibrationColorCreation.Fit(repeated, CalibrationType.LumFourColor, 16));
        Assert.Throws<InvalidDataException>(() => CalibrationColorCreation.Fit(new[] { new CalibrationColorSample(new[] { 10d, 20, 30 }, 1, 0.3, 0) }, CalibrationType.LumOneColor, 16));
    }

    [Fact]
    public void LuminanceFitRequiresOnlyYAndDoesNotRequireUnusedChromaticity()
    {
        var samples = new[] { new CalibrationColorSample(new[] { 10d }, 25, double.NaN, double.NaN),
            new CalibrationColorSample(new[] { 20d }, 50, double.NaN, double.NaN) };
        var fit = CalibrationColorCreation.Fit(samples, CalibrationType.Luminance, 16);
        Assert.Equal(2.5, fit.Json["a"]!.Value<double>(), 10);
        Assert.Equal(0, fit.RelativeRmsError, 10);
    }

    [Theory]
    [InlineData(CalibrationType.LumFourColor)]
    [InlineData(CalibrationType.LumMultiColor)]
    public void IndependentPureRgbSamplesMayContainZeroIndividualChannels(CalibrationType type)
    {
        var samples = new[]
        {
            Sample(new[] { 10d, 0, 0 }, 20, 10, 10),
            Sample(new[] { 0d, 10, 0 }, 10, 30, 10),
            Sample(new[] { 0d, 0, 10 }, 10, 10, 40)
        };
        double[] matrix = { 2, 1, 1, 1, 3, 1, 1, 1, 4 };
        var fit = CalibrationColorCreation.Fit(samples, type, 16);
        for (int i = 0; i < matrix.Length; i++) Assert.Equal(matrix[i], type == CalibrationType.LumMultiColor
            ? fit.Json["pa"]![i]!.Value<double>() : fit.Json[((char)('a' + i)).ToString()]!.Value<double>(), 10);
        Assert.Equal(0, fit.RelativeRmsError, 10);
    }

    [Fact]
    public void MissingRequiredRegressorAndZeroMonoSampleCannotProduceAUsableFit()
    {
        var noBlue = new[]
        {
            Sample(new[] { 10d, 0, 0 }, 20, 10, 10),
            Sample(new[] { 0d, 10, 0 }, 10, 30, 10),
            Sample(new[] { 10d, 10, 0 }, 30, 40, 20)
        };
        foreach (var type in new[] { CalibrationType.LumFourColor, CalibrationType.LumMultiColor, CalibrationType.LumOneColor })
            Assert.Throws<InvalidDataException>(() => CalibrationColorCreation.Fit(noBlue, type, 16));
        Assert.Throws<InvalidDataException>(() => CalibrationColorCreation.Fit(
            new[] { new CalibrationColorSample(new[] { 0d }, 25, double.NaN, double.NaN) }, CalibrationType.Luminance, 16));
    }

    [Fact]
    public void ChessboardCreationRejectsInvalidInputsBeforeNativeImageLoading()
    {
        Assert.Throws<ArgumentException>(() => CalibrationDistortionCreation.Fit(Array.Empty<string>(), 2, 6, 1));
        Assert.Throws<ArgumentException>(() => CalibrationDistortionCreation.Fit(Array.Empty<string>(), 9, 6, double.NaN));
        Assert.Throws<InvalidDataException>(() => CalibrationDistortionCreation.Fit(Array.Empty<string>(), 9, 6, 1));
    }

    [Fact]
    public void ChessboardCreationDetectsSyntheticPerspectiveImagesAndProducesLoadableParameters()
    {
        string folder = Path.Combine(Path.GetTempPath(), "ColorVisionChessboardTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var board = new Mat(480, 640, MatType.CV_8UC1, Scalar.White);
            for (int row = 0; row < 7; row++)
                for (int col = 0; col < 10; col++)
                    if ((row + col) % 2 == 0) Cv2.Rectangle(board, new Rect(120 + col * 40, 100 + row * 40, 40, 40), Scalar.Black, -1);
            Point2f[] original = { new(120, 100), new(520, 100), new(520, 380), new(120, 380) };
            Point2f[][] poses =
            {
                new Point2f[] { new(110, 90), new(530, 110), new(510, 390), new(130, 360) },
                new Point2f[] { new(150, 110), new(490, 70), new(530, 340), new(120, 390) },
                new Point2f[] { new(100, 140), new(510, 100), new(470, 400), new(150, 350) },
                new Point2f[] { new(180, 80), new(540, 150), new(460, 390), new(110, 310) },
                new Point2f[] { new(100, 70), new(480, 130), new(540, 350), new(160, 410) }
            };
            var paths = new List<string>();
            for (int i = 0; i < poses.Length; i++)
            {
                using var transform = Cv2.GetPerspectiveTransform(original, poses[i]);
                using var image = new Mat();
                Cv2.WarpPerspective(board, image, transform, new Size(640, 480), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.White);
                string path = Path.Combine(folder, $"board-{i}.png");
                Assert.True(Cv2.ImWrite(path, image)); paths.Add(path);
            }
            var fit = CalibrationDistortionCreation.Fit(paths, 9, 6, 1);
            Assert.Equal(5, fit.ImageCount);
            Assert.True(double.IsFinite(fit.ReprojectionRmsPixels));
            Assert.Equal(9, ((JArray)fit.Json["cameraMatrix"]!).Count);
            Assert.Equal(5, ((JArray)fit.Json["distCoeffs"]!).Count);
            Assert.Empty(ColorVision.Engine.Services.PhyCameras.Calibration.Editing.CalibrationJsonDocument.Parse(
                ColorVision.Engine.Services.Types.ServiceTypes.Distortion, fit.Json.ToString()).Validate());
        }
        finally { Directory.Delete(folder, true); }
    }

    private static CalibrationColorSample Sample(double[] raw, double x, double y, double z)
        => new(raw, y, x / (x + y + z), y / (x + y + z));
}
