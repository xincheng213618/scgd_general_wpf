#pragma warning disable CA1707
using System.IO;
using System.Security.Cryptography;

namespace Spectrum.Tests;

public sealed class SpectrumCalibrationStateTests
{
    [Fact]
    public void TryCreateCalibrationSnapshot_ValidFilesCapturesNormalizedPathsAndHashes()
    {
        using var files = CalibrationFiles.Create();

        bool success = SpectrometerManager.TryCreateCalibrationSnapshot(
            "default",
            files.WavelengthPath,
            files.MagnitudePath,
            out SpectrumCalibrationSnapshot? snapshot,
            out string errorMessage);

        Assert.True(success, errorMessage);
        SpectrumCalibrationSnapshot value = Assert.IsType<SpectrumCalibrationSnapshot>(snapshot);
        Assert.Equal("default", value.GroupName);
        Assert.Equal(Path.GetFullPath(files.WavelengthPath), value.WavelengthPath);
        Assert.Equal(Path.GetFullPath(files.MagnitudePath), value.MagnitudePath);
        Assert.Equal(ComputeSha256(files.WavelengthPath), value.WavelengthSha256);
        Assert.Equal(ComputeSha256(files.MagnitudePath), value.MagnitudeSha256);
        Assert.True(value.MatchesConfigured("default", files.WavelengthPath, files.MagnitudePath));
        Assert.False(value.MatchesConfigured("other", files.WavelengthPath, files.MagnitudePath));
        Assert.False(value.MatchesConfigured("default", files.MagnitudePath, files.WavelengthPath));
    }

    [Fact]
    public void TryCreateCalibrationSnapshot_ChangedValidContentProducesDifferentHash()
    {
        using var files = CalibrationFiles.Create();
        Assert.True(SpectrometerManager.TryCreateCalibrationSnapshot(
            "default",
            files.WavelengthPath,
            files.MagnitudePath,
            out SpectrumCalibrationSnapshot? before,
            out string beforeError), beforeError);

        WriteWavelengthFile(files.WavelengthPath, [381d, 780d], FileMode.Create);

        Assert.True(SpectrometerManager.TryCreateCalibrationSnapshot(
            "default",
            files.WavelengthPath,
            files.MagnitudePath,
            out SpectrumCalibrationSnapshot? after,
            out string afterError), afterError);
        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.NotEqual(before.WavelengthSha256, after.WavelengthSha256);
        Assert.Equal(before.MagnitudeSha256, after.MagnitudeSha256);
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void TryCreateCalibrationSnapshot_InvalidMagnitudeFileReturnsNoSnapshot()
    {
        using var files = CalibrationFiles.Create();
        File.WriteAllBytes(files.MagnitudePath, [0x01, 0x02, 0x03]);

        bool success = SpectrometerManager.TryCreateCalibrationSnapshot(
            "default",
            files.WavelengthPath,
            files.MagnitudePath,
            out SpectrumCalibrationSnapshot? snapshot,
            out string errorMessage);

        Assert.False(success);
        Assert.Null(snapshot);
        Assert.Contains("幅值文件无效", errorMessage);
    }

    [Fact]
    public void AutodarkParam_CountAndEndpointRoundTrip()
    {
        var parameter = new AutodarkParam
        {
            fTimeStart = 50,
            nStepTime = 100,
            nStepCount = 3,
        };

        Assert.Equal(250, parameter.nEndTime);

        parameter.nStepCount = 1;
        Assert.Equal(50, parameter.nEndTime);

        parameter.nEndTime = 250;
        Assert.Equal(3, parameter.nStepCount);
        Assert.Equal(250, parameter.nEndTime);
    }

    [Fact]
    public void AutodarkParam_InvalidRangesClampToSingleMeasurement()
    {
        var parameter = new AutodarkParam
        {
            fTimeStart = 50,
            nStepTime = 100,
        };

        parameter.nStepCount = 0;
        Assert.Equal(1, parameter.nStepCount);
        Assert.Equal(50, parameter.nEndTime);

        parameter.nEndTime = -100;
        Assert.Equal(1, parameter.nStepCount);

        parameter.nStepCount = 5;
        parameter.nStepTime = 0;
        parameter.nEndTime = 500;
        Assert.Equal(1, parameter.nStepCount);
        Assert.Equal(50, parameter.nEndTime);
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void WriteWavelengthFile(
        string path,
        IReadOnlyList<double> wavelengths,
        FileMode mode = FileMode.CreateNew)
    {
        using var stream = new FileStream(path, mode, FileAccess.Write);
        using var writer = new BinaryWriter(stream);
        writer.Write(checked((ulong)(sizeof(ulong) + wavelengths.Count * sizeof(double))));
        foreach (double wavelength in wavelengths)
            writer.Write(wavelength);
    }

    private static void WriteMagnitudeFile(
        string path,
        IReadOnlyList<double> wavelengths,
        IReadOnlyList<double> coefficients)
    {
        Assert.Equal(wavelengths.Count, coefficients.Count);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        using var writer = new BinaryWriter(stream);
        writer.Write(checked((ulong)(24 + wavelengths.Count * 2 * sizeof(double))));
        writer.Write(4f);
        writer.Write(683);
        writer.Write(checked((ulong)wavelengths.Count));
        foreach (double wavelength in wavelengths)
            writer.Write(wavelength);
        foreach (double coefficient in coefficients)
            writer.Write(coefficient);
    }

    private sealed class CalibrationFiles : IDisposable
    {
        public string WavelengthPath { get; }
        public string MagnitudePath { get; }

        private CalibrationFiles(string wavelengthPath, string magnitudePath)
        {
            WavelengthPath = wavelengthPath;
            MagnitudePath = magnitudePath;
        }

        public static CalibrationFiles Create()
        {
            string prefix = Path.Combine(Path.GetTempPath(), $"spectrum-calibration-{Guid.NewGuid():N}");
            string wavelengthPath = $"{prefix}-wave.dat";
            string magnitudePath = $"{prefix}-magnitude.dat";
            WriteWavelengthFile(wavelengthPath, [380d, 780d]);
            WriteMagnitudeFile(magnitudePath, [380d, 780d], [1d, 1d]);
            return new CalibrationFiles(wavelengthPath, magnitudePath);
        }

        public void Dispose()
        {
            if (File.Exists(WavelengthPath))
                File.Delete(WavelengthPath);
            if (File.Exists(MagnitudePath))
                File.Delete(MagnitudePath);
        }
    }
}
