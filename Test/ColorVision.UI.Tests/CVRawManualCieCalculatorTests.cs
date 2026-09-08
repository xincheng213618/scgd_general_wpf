using ColorVision.Engine.Media;
using ColorVision.Engine.Services.PhyCameras.Calibration;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.POI;
using ColorVision.FileIO;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CVRawManualCieCalculatorTests
{
    [Fact]
    public void CalibrationSlotsFollowNativeExecutionOrderInsteadOfEnumOrder()
    {
        string[] actual = CalibrationSlotDefinitions.NormalSlots.Select(slot => slot.Key).ToArray();

        Assert.Equal(new[]
        {
            nameof(GroupResource.DarkNoise),
            nameof(GroupResource.DefectPoint),
            nameof(GroupResource.DSNU),
            nameof(GroupResource.Uniformity),
            nameof(GroupResource.ColorShift),
            nameof(GroupResource.Distortion),
            nameof(GroupResource.LineArity),
            nameof(GroupResource.ColorDiff),
            nameof(GroupResource.AngleShift),
        }, actual);
    }

    [Fact]
    public void MissingCalibrationFilePreservesTemplateSelection()
    {
        CalibrationBase calibration = new(new List<ColorVision.Engine.ModDetailModel>())
        {
            IsExitFile = true,
            IsSelected = true,
        };

        calibration.IsExitFile = false;

        Assert.True(calibration.IsSelected);
    }

    [Fact]
    public void CorrectSinglePointMatchesProvidedMatlabEquationsAndPreservesNormalization()
    {
        CVRawManualCieConfig source = CreateProvidedCalibration();
        ColorCorrectionMeasurement measurement = new(
            new ColorCorrectionYxy(1237.00537109375, 0.194746896624565, 0.875731885433197),
            new ColorCorrectionYxy(1324, 0.218552, 0.742315));

        CVRawManualCieConfig corrected = LumFourColorCorrectionCalculator.CorrectSinglePoint(source, measurement);

        AssertMatrix(corrected,
            2.297879633355055, 0.108451436541735, 0.761727111388546,
            0.667226773253470, 1.135931797729968, -0.090521558009622,
            0.109418026828470, -0.646927555640377, 4.661669659111825);
        Assert.Equal(source.Gain_x, corrected.Gain_x);
        Assert.Equal(source.Gain_y, corrected.Gain_y);
        Assert.Equal(source.Gain_z, corrected.Gain_z);
        Assert.Equal(source.Texp_x, corrected.Texp_x);
        Assert.Equal(source.Texp_y, corrected.Texp_y);
        Assert.Equal(source.Texp_z, corrected.Texp_z);
    }

    [Fact]
    public void CorrectFourColorMatchesProvidedRgbwDataIncludingNegativeRawIntermediate()
    {
        CVRawManualCieConfig source = CreateProvidedCalibration();
        LumFourColorCorrectionMeasurements measurements = new(
            CreateMeasurement(126.78367, 0.6933417, 0.30602154, 89.2225, 0.6887435, 0.3085066),
            CreateMeasurement(720.7425, 0.18320304, 0.62499464, 494.32455, 0.13895464, 0.7421502),
            CreateMeasurement(46.2292, 0.1433485, 0.03912296, 49.398224, 0.14334978, 0.036007576),
            CreateMeasurement(300.52304, 0.3194248, 0.3445272, 212.77502, 0.29537222, 0.34703013));

        CVRawManualCieConfig corrected = LumFourColorCorrectionCalculator.CorrectFourColor(source, measurements);

        AssertMatrix(corrected,
            0.701621476635818, -0.034170929639462, 0.507551566230807,
            0.197746016614588, 0.764889756012714, -0.036601927645408,
            0.069169579223746, -0.692610255900422, 2.998908394068809);
    }

    [Fact]
    public void CorrectSinglePointAcceptsFiniteNegativeYxyValues()
    {
        CVRawManualCieConfig source = CreateIdentityConfig();
        ColorCorrectionMeasurement measurement = new(
            new ColorCorrectionYxy(2, -0.25, 0.5),
            new ColorCorrectionYxy(3, 0.25, -0.5));

        CVRawManualCieConfig corrected = LumFourColorCorrectionCalculator.CorrectSinglePoint(source, measurement);

        AssertMatrix(corrected, 1.5, 0, 0, 0, 1.5, 0, 0, 0, -2.5);
    }

    [Fact]
    public void CorrectionRejectsZeroChromaticityYAndSingularCalibration()
    {
        ColorCorrectionMeasurement zeroY = new(new ColorCorrectionYxy(1, 0.2, 0), new ColorCorrectionYxy(1, 0.2, 0.3));
        Assert.Throws<InvalidOperationException>(() => LumFourColorCorrectionCalculator.CorrectSinglePoint(CreateIdentityConfig(), zeroY));

        CVRawManualCieConfig singular = CreateIdentityConfig();
        singular.I = 0;
        ColorCorrectionMeasurement valid = new(new ColorCorrectionYxy(1, 0.2, 0.3), new ColorCorrectionYxy(1, 0.2, 0.3));
        Assert.Throws<InvalidOperationException>(() => LumFourColorCorrectionCalculator.CorrectSinglePoint(singular, valid));
    }

    [Fact]
    public void CalibrationSessionUsesSingleOrRgbwCaptureOrder()
    {
        LumFourColorCalibrationSession session = new();

        session.SetMode(false);

        Assert.False(session.IsSinglePoint);
        Assert.Equal(new[] { "R", "G", "B", "W" }, session.Samples.Select(sample => sample.Name));

        session.SetMode(true);

        Assert.True(session.IsSinglePoint);
        Assert.Equal("单点", Assert.Single(session.Samples).Name);
    }

    [Fact]
    public void CalibrationSampleRetainsFiniteNegativeMeasurementsAndSpectrum()
    {
        LumFourColorCalibrationSession session = new();
        session.SetMode(true);
        LumFourColorCalibrationSample sample = session.Samples[0];
        sample.SetSpectrumMeasurement(new LumFourColorSpectrumCapture(
            new ColorCorrectionYxy(-4, -0.2, -0.4),
            new[] { new ColorCorrectionSpectrumPoint(380, -0.01) },
            12,
            DateTimeOffset.UnixEpoch));
        Assert.False(session.IsComplete);
        sample.SetCameraMeasurement(
            new PoiMeasurementPoint(10, 20, 30, 40, PoiMeasurementShape.Rect),
            new PoiMeasurementResult(-1, -2, -3, -0.25f, -0.5f, 0, 0, 0, 0));

        ColorCorrectionMeasurement measurement = sample.CreateMeasurement();

        Assert.True(session.IsComplete);
        Assert.Equal(-2, measurement.Camera.Y);
        Assert.Equal(-0.25, measurement.Camera.CieX, 6);
        Assert.Equal(-4, measurement.Reference.Y);
        Assert.Equal(-0.01, Assert.Single(measurement.Spectrum!).Value);
    }

    [Fact]
    public void RetakingImageInvalidatesPoiButPreservesSpectrumForThatSample()
    {
        LumFourColorCalibrationSession session = new();
        session.SetMode(true);
        LumFourColorCalibrationSample sample = session.Samples[0];
        sample.SetCameraMeasurement(
            new PoiMeasurementPoint(0, 0, 1, 1, PoiMeasurementShape.Rect),
            new PoiMeasurementResult(1, 2, 3, 0.2f, 0.3f, 0, 0, 0, 0));
        sample.SetSpectrumMeasurement(new LumFourColorSpectrumCapture(
            new ColorCorrectionYxy(4, 0.2, 0.3),
            new[] { new ColorCorrectionSpectrumPoint(380, 1) },
            13,
            DateTimeOffset.UnixEpoch));

        sample.SetFrame(new LumFourColorCieCapture(new byte[12], 1, 1, 32, 3, 1, new[] { 1f }), null!);

        Assert.True(sample.HasImage);
        Assert.False(sample.HasCameraMeasurement);
        Assert.True(sample.HasSpectrumMeasurement);
        Assert.Equal(13, sample.SpectrumResultId);
        Assert.False(session.IsComplete);
    }

    [Fact]
    public void WorkflowLoadsEmbeddedCieInsteadOfFollowingAssociatedRawFile()
    {
        string root = Path.Combine(Path.GetTempPath(), $"FourColorCie-{Guid.NewGuid():N}");
        string ciePath = Path.Combine(root, "sample.cvcie");
        string sourcePath = Path.Combine(root, "sample.cvraw");
        Directory.CreateDirectory(root);
        File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3 });
        float[] xyz = { -1, 2, 3 };
        byte[] data = new byte[xyz.Length * sizeof(float)];
        Buffer.BlockCopy(xyz, 0, data, 0, data.Length);
        using CVCIEFile file = new()
        {
            Version = 1,
            SrcFileName = Path.GetFileName(sourcePath),
            Gain = 1,
            Channels = 3,
            Exp = new[] { 1f, 1f, 1f },
            Cols = 1,
            Rows = 1,
            Bpp = 32,
            Data = data,
        };

        try
        {
            Assert.True(CVFileUtil.WriteCIEFile(ciePath, file));

            LumFourColorCieCapture capture = LumFourColorCieService.Load(ciePath);

            Assert.Equal(32, capture.BitsPerChannel);
            Assert.Equal(3, capture.Channels);
            Assert.Equal(data, capture.Data);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SerializeCalibrationFileRoundTripsThroughExistingLoaderAndKeepsNegativeCoefficients()
    {
        CVRawManualCieConfig calibration = CreateIdentityConfig();
        calibration.F = -0.25;
        string jsonText = LumFourColorCorrectionCalculator.SerializeCalibrationFile(calibration);
        JObject json = JObject.Parse(jsonText);
        string path = Path.GetTempFileName();

        try
        {
            File.WriteAllText(path, jsonText);
            bool loaded = CVRawManualCieCalculator.TryLoadLumFourColorCalibrationDefaults(path, out CVRawManualCieConfig roundTripped, out string? error);

            Assert.True(loaded, error);
            Assert.Equal(-0.25, roundTripped.F);
            Assert.Equal(-0.25, json.Value<double>("f"));
            Assert.Equal(1, json.Value<double>("Gain_x"));
            Assert.Null(json["F"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    public void CalculatePreservesBgrInputPlanarXyzAndNegativeCalibrationCoefficients(int bpp)
    {
        using CVCIEFile raw = CreateRaw(bpp);
        byte[] original = (byte[])raw.Data.Clone();
        CVRawManualCieConfig config = CreateIdentityConfig();
        config.A = -1;

        CVRawManualCieCalculator.CalculationResult result = CVRawManualCieCalculator.Calculate(raw, config);

        float scale = bpp == 8 ? 1 : 100;
        Assert.Equal(new[] { -30 * scale, -60 * scale, 20 * scale, 50 * scale, 10 * scale, 40 * scale }, ReadXyz(result));
        Assert.Equal(original, raw.Data);
        Assert.Equal(2, result.Width);
        Assert.Equal(1, result.Height);
        Assert.Equal(new[] { 1f, 1f, 1f }, result.Exposure);
    }

    [Theory]
    [InlineData(8, 5)]
    [InlineData(8, 7)]
    [InlineData(16, 11)]
    [InlineData(16, 13)]
    public void CalculateRejectsTruncatedOrOversizedPayload(int bpp, int length)
    {
        using CVCIEFile raw = CreateRaw(bpp);
        raw.Data = new byte[length];

        Assert.Throws<InvalidOperationException>(() => CVRawManualCieCalculator.Calculate(raw, CreateIdentityConfig()));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, -2)]
    public void CalculateRejectsInvalidDimensions(int rows, int cols)
    {
        using CVCIEFile raw = CreateRaw();
        raw.Rows = rows;
        raw.Cols = cols;

        Assert.Throws<InvalidOperationException>(() => CVRawManualCieCalculator.Calculate(raw, CreateIdentityConfig()));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void CalculateRejectsNonFiniteCalibrationInsteadOfFallingBack(double invalidValue)
    {
        using CVCIEFile raw = CreateRaw();
        CVRawManualCieConfig matrixConfig = CreateIdentityConfig();
        matrixConfig.A = invalidValue;
        CVRawManualCieConfig gainConfig = CreateIdentityConfig();
        gainConfig.Gain_y = invalidValue;
        CVRawManualCieConfig exposureConfig = CreateIdentityConfig();
        exposureConfig.Texp_z = invalidValue;

        Assert.Throws<InvalidOperationException>(() => CVRawManualCieCalculator.Calculate(raw, matrixConfig));
        Assert.Throws<InvalidOperationException>(() => CVRawManualCieCalculator.Calculate(raw, gainConfig));
        Assert.Throws<InvalidOperationException>(() => CVRawManualCieCalculator.Calculate(raw, exposureConfig));
    }

    [Theory]
    [InlineData(1e300)]
    [InlineData(1e-300)]
    public void CalculateRejectsExposureThatCannotBeRepresentedAsPositiveFloat(double exposure)
    {
        using CVCIEFile raw = CreateRaw();
        CVRawManualCieConfig config = CreateIdentityConfig();
        config.Texp_x = exposure;

        Assert.Throws<InvalidOperationException>(() => CVRawManualCieCalculator.Calculate(raw, config));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void CalculateRejectsInvalidSourceNormalizationWhenFallbackIsSelected(float invalidValue)
    {
        using CVCIEFile raw = CreateRaw();
        CVRawManualCieConfig config = CreateIdentityConfig();
        raw.Exp = new[] { invalidValue };
        Assert.Throws<InvalidOperationException>(() => CVRawManualCieCalculator.Calculate(raw, config));

        raw.Exp = new[] { 1f };
        raw.Gain = invalidValue;
        config.Gain_x = 0;
        Assert.Throws<InvalidOperationException>(() => CVRawManualCieCalculator.Calculate(raw, config));
    }

    [Fact]
    public void CalculateKeepsFiniteNonPositiveConfigurationAsSourceNormalizationFallback()
    {
        using CVCIEFile raw = CreateRaw();
        raw.Exp = new[] { 2f };
        raw.Gain = 2f;
        CVRawManualCieConfig config = CreateIdentityConfig();
        config.Gain_x = 0;
        config.Gain_y = -1;
        config.Gain_z = 0;
        config.Texp_y = -1;

        CVRawManualCieCalculator.CalculationResult result = CVRawManualCieCalculator.Calculate(raw, config);

        Assert.Equal(new[] { 7.5f, 15f, 5f, 12.5f, 2.5f, 10f }, ReadXyz(result));
        Assert.Equal(new[] { 2f, 2f, 2f }, result.Exposure);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    public void CalculateRejectsFiniteCoefficientsWhoseOutputOverflowsCieFloat(int bpp)
    {
        using CVCIEFile raw = CreateRaw(bpp);
        CVRawManualCieConfig config = CreateIdentityConfig();
        config.A = float.MaxValue;

        Assert.Throws<InvalidOperationException>(() => CVRawManualCieCalculator.Calculate(raw, config));
    }

    [Theory]
    [InlineData("a", "NaN")]
    [InlineData("Gain_x", "Infinity")]
    [InlineData("Texp_x", "-Infinity")]
    [InlineData("i", "1e9999")]
    public void ImportRejectsNonFiniteCalibrationNumbers(string propertyName, string invalidValue)
    {
        JObject calibration = new()
        {
            ["Gain_x"] = 1, ["Gain_y"] = 1, ["Gain_z"] = 1,
            ["Texp_x"] = 1, ["Texp_y"] = 1, ["Texp_z"] = 1,
            ["a"] = 1, ["b"] = 0, ["c"] = 0,
            ["d"] = 0, ["e"] = 1, ["f"] = 0,
            ["g"] = 0, ["h"] = 0, ["i"] = 1
        };
        calibration[propertyName] = invalidValue;
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, calibration.ToString());

            bool imported = CVRawManualCieCalculator.TryLoadLumFourColorCalibrationDefaults(path, out _, out string? error);

            Assert.False(imported);
            Assert.Contains(propertyName, error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static CVRawManualCieConfig CreateIdentityConfig() => new()
    {
        A = 1, B = 0, C = 0,
        D = 0, E = 1, F = 0,
        G = 0, H = 0, I = 1
    };

    private static CVRawManualCieConfig CreateProvidedCalibration() => new()
    {
        Gain_x = 1,
        Gain_y = 1,
        Gain_z = 1,
        A = 1.0215563462941002,
        B = 0.10267037814756194,
        C = 0.56726676658794128,
        D = 0.29662551934032694,
        E = 1.0753803303726448,
        F = -0.067412428671903793,
        G = 0.048643400227649437,
        H = -0.61244273154421425,
        I = 3.4715979294856516
    };

    private static ColorCorrectionMeasurement CreateMeasurement(
        double cameraY, double cameraX, double cameraChromaticityY,
        double referenceY, double referenceX, double referenceChromaticityY) => new(
            new ColorCorrectionYxy(cameraY, cameraX, cameraChromaticityY),
            new ColorCorrectionYxy(referenceY, referenceX, referenceChromaticityY));

    private static void AssertMatrix(CVRawManualCieConfig actual, params double[] expected)
    {
        double[] values = [actual.A, actual.B, actual.C, actual.D, actual.E, actual.F, actual.G, actual.H, actual.I];
        Assert.Equal(expected.Length, values.Length);
        for (int index = 0; index < values.Length; index++)
        {
            Assert.InRange(Math.Abs(expected[index] - values[index]), 0, 1e-7 * Math.Max(1, Math.Abs(expected[index])));
        }
    }

    private static CVCIEFile CreateRaw(int bpp = 8)
    {
        byte[] data = [10, 20, 30, 40, 50, 60];
        if (bpp == 16)
        {
            ushort[] pixels = [1000, 2000, 3000, 4000, 5000, 6000];
            data = new byte[pixels.Length * sizeof(ushort)];
            Buffer.BlockCopy(pixels, 0, data, 0, data.Length);
        }
        return new CVCIEFile
        {
            FileExtType = CVType.Raw,
            Cols = 2,
            Rows = 1,
            Channels = 3,
            Bpp = bpp,
            Gain = 1,
            Exp = [1f],
            Data = data
        };
    }

    private static float[] ReadXyz(CVRawManualCieCalculator.CalculationResult result)
    {
        float[] xyz = new float[result.XyzData.Length / sizeof(float)];
        Buffer.BlockCopy(result.XyzData, 0, xyz, 0, result.XyzData.Length);
        return xyz;
    }
}
