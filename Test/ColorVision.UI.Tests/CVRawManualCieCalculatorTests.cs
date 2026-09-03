using ColorVision.Engine.Media;
using ColorVision.FileIO;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CVRawManualCieCalculatorTests
{
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
