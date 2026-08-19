using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Spectrum;
using System.IO;

namespace Spectrum.Tests;

public class SpectrumCorrectionEnginePolicyTests
{
    [Fact]
    public void CaptureTimeoutCoversConfiguredAutoIntegrationAverageAndDarkCycle()
    {
        TimeSpan timeout = DeviceSpectrum.CalculateCorrectionCaptureTimeout(
            integrationTimeMilliseconds: 100,
            maximumIntegrationTimeMilliseconds: 60_000,
            average: 10,
            autoIntegration: true,
            includesDarkMeasurement: true);

        Assert.Equal(TimeSpan.FromSeconds(1_230), timeout);
    }

    [Fact]
    public void CaptureTimeoutRetainsMinimumForShortAcquisition()
    {
        TimeSpan timeout = DeviceSpectrum.CalculateCorrectionCaptureTimeout(
            integrationTimeMilliseconds: 100,
            maximumIntegrationTimeMilliseconds: 60_000,
            average: 1,
            autoIntegration: false,
            includesDarkMeasurement: false);

        Assert.Equal(TimeSpan.FromSeconds(35), timeout);
    }

    [Fact]
    public void WavelengthMetadataUsesValidAxisAndCropsServicePadding()
    {
        var metadata = DeviceSpectrum.ResolveCorrectionWavelengthMetadata(380, 780, 0.1, 10_000);

        Assert.Equal(380, metadata.Start);
        Assert.Equal(780, metadata.End);
        Assert.Equal(0.1, metadata.Interval);
        Assert.Equal(4_001, metadata.PointCount);
    }

    public static TheoryData<double?, double?, double?> InvalidWavelengthMetadata => new()
    {
        { null, 780d, 0.1d },
        { 380d, null, 0.1d },
        { 380d, 780d, null },
        { 380d, 780d, 0d },
        { 780d, 380d, 0.1d },
        { 380d, 780.05d, 0.1d },
    };

    [Theory]
    [MemberData(nameof(InvalidWavelengthMetadata))]
    public void WavelengthMetadataFailsClosedInsteadOfGuessingDefaults(
        double? start,
        double? end,
        double? interval)
    {
        Assert.Throws<InvalidOperationException>(() =>
            DeviceSpectrum.ResolveCorrectionWavelengthMetadata(start, end, interval, 10_000));
    }

    [Fact]
    public void WavelengthMetadataRejectsShortSpectrum()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DeviceSpectrum.ResolveCorrectionWavelengthMetadata(380, 780, 0.1, 4_000));
    }

    [Theory]
    [InlineData(381, 780, 0.1)]
    [InlineData(380, 779, 0.1)]
    [InlineData(380, 780, 1)]
    public void WavelengthMetadataRejectsNonCanonicalServiceAxis(
        double start,
        double end,
        double interval)
    {
        Assert.Throws<InvalidOperationException>(() =>
            DeviceSpectrum.ResolveCorrectionWavelengthMetadata(start, end, interval, 10_000));
    }

    [Fact]
    public void MagnitudeValidationRejectsNonCanonicalFourThousandAndOnePointGrid()
    {
        string path = Path.Combine(Path.GetTempPath(), $"spectrum-noncanonical-{Guid.NewGuid():N}.dat");
        try
        {
            using (FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write))
            using (BinaryWriter writer = new(stream))
            {
                const int count = 4001;
                writer.Write((ulong)(24 + count * 2 * sizeof(double)));
                writer.Write(4f);
                writer.Write(683);
                writer.Write((ulong)count);
                for (int index = 0; index < count; index++)
                    writer.Write(381d + 0.1d * index);
                for (int index = 0; index < count; index++)
                    writer.Write(1d);
            }

            string? error = DeviceSpectrum.ValidateCorrectionMagnitudeFile(path);

            Assert.NotNull(error);
            Assert.Contains("380.0", error);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Theory]
    [InlineData(DeviceStatusType.Opened, true)]
    [InlineData(DeviceStatusType.Free, true)]
    [InlineData(DeviceStatusType.LiveOpened, true)]
    [InlineData(DeviceStatusType.Busy, false)]
    [InlineData(DeviceStatusType.SP_Continuous_Mode, false)]
    [InlineData(DeviceStatusType.Closed, false)]
    public void CorrectionCaptureReadyStateIsNarrow(DeviceStatusType status, bool expected)
    {
        Assert.Equal(expected, DeviceSpectrum.IsCorrectionCaptureReadyStatus(status));
    }

}
