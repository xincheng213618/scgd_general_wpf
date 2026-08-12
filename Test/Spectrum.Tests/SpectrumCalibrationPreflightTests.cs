#pragma warning disable CA1707
using ColorVision.Engine.Services.Devices.Spectrum;
using cvColorVision;
using System.IO;
using PluginCalibrationFileValidator = Spectrum.Configs.CalibrationFileValidator;

namespace Spectrum.Tests;

public sealed class SpectrumCalibrationPreflightTests
{
    [Fact]
    public void MeasurementPreflight_AcceptsValidCalibrationFiles()
    {
        string wavelengthPath = NewTempPath("wave");
        string magnitudePath = NewTempPath("magnitude");
        try
        {
            WriteWavelengthFile(wavelengthPath, [380d, 780d]);
            WriteMagnitudeFile(magnitudePath, [380d, 780d], [1d, 1d]);

            string? error = DeviceSpectrum.ValidateMeasurementCalibrationFiles(wavelengthPath, magnitudePath);

            Assert.Null(error);
        }
        finally
        {
            DeleteIfExists(wavelengthPath);
            DeleteIfExists(magnitudePath);
        }
    }

    [Fact]
    public void MeasurementPreflight_ReportsBothMissingFiles()
    {
        string wavelengthPath = NewTempPath("missing-wave");
        string magnitudePath = NewTempPath("missing-magnitude");

        string? error = DeviceSpectrum.ValidateMeasurementCalibrationFiles(wavelengthPath, magnitudePath);

        Assert.NotNull(error);
        Assert.Contains("波长标定文件：文件不存在", error);
        Assert.Contains(wavelengthPath, error);
        Assert.Contains("幅值标定文件：文件不存在", error);
        Assert.Contains(magnitudePath, error);
    }

    [Fact]
    public void MeasurementPreflight_ReportsMalformedFileHeader()
    {
        string wavelengthPath = NewTempPath("bad-wave");
        string magnitudePath = NewTempPath("magnitude");
        try
        {
            using (var stream = new FileStream(wavelengthPath, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
                writer.Write(999UL);
            WriteMagnitudeFile(magnitudePath, [380d, 780d], [1d, 1d]);

            string? error = DeviceSpectrum.ValidateMeasurementCalibrationFiles(wavelengthPath, magnitudePath);

            Assert.NotNull(error);
            Assert.Contains("波长标定文件", error);
            Assert.Contains("格式不匹配", error);
            Assert.DoesNotContain("幅值标定文件", error);
        }
        finally
        {
            DeleteIfExists(wavelengthPath);
            DeleteIfExists(magnitudePath);
        }
    }

    [Fact]
    public void PluginValidatorFacade_UsesTheSharedFormatRules()
    {
        string path = NewTempPath("magnitude");
        try
        {
            WriteMagnitudeFile(path, [380d, 780d], [1d, 1d]);

            SpectrumCalibrationFileValidationResult shared = SpectrumCalibrationFileValidator.ValidateMaguideFile(path);
            Spectrum.Configs.CalibrationFileValidationResult plugin = PluginCalibrationFileValidator.ValidateMaguideFile(path);

            Assert.Equal(shared.IsValid, plugin.IsValid);
            Assert.Equal(shared.Message, plugin.Message);
            Assert.Equal(shared.DataCount, plugin.DataCount);
            Assert.Equal(shared.MagExpTime, plugin.MagExpTime);
            Assert.Equal(shared.LvCoefficient, plugin.LvCoefficient);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    private static string NewTempPath(string label) =>
        Path.Combine(Path.GetTempPath(), $"spectrum-{label}-{Guid.NewGuid():N}.dat");

    private static void WriteWavelengthFile(string path, IReadOnlyList<double> wavelengths)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
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

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
