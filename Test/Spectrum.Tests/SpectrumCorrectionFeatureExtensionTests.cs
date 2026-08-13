#pragma warning disable CA1707
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Correction;
using Spectrum.Calibration.Correction;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;

namespace Spectrum.Tests;

public class SpectrumCorrectionFeatureExtensionTests
{
    [Fact]
    public void EngineDevice_ExposesBuiltInCorrectionCommand()
    {
        PropertyInfo property = Assert.IsAssignableFrom<PropertyInfo>(
            typeof(DeviceSpectrum).GetProperty(nameof(DeviceSpectrum.OpenSpectrumCorrectionCommand)));
        CommandDisplayAttribute attribute = Assert.IsType<CommandDisplayAttribute>(
            property.GetCustomAttribute<CommandDisplayAttribute>());

        Assert.Equal("光谱校正", attribute.DisplayName);
        Assert.Equal(-3, attribute.Order);
    }

    [Fact]
    public void SpectrumPlugin_ExposesIndependentCorrectionMenu()
    {
        var menu = new MenuSpectrumCorrection();

        Assert.Equal("光谱校正", menu.Header);
        Assert.Equal("Spectrum", menu.TargetName);
        Assert.Equal(ColorVision.UI.Menus.MenuItemConstants.Tool, menu.OwnerGuid);
    }

    [Fact]
    public void ReflectionProviderChain_IsNoLongerPresent()
    {
        Assert.Null(typeof(DeviceSpectrum).Assembly.GetType(
            "ColorVision.Engine.Services.Devices.Spectrum.SpectrumCorrectionFeatureProviderRegistry"));
        Assert.Null(typeof(MenuSpectrumCorrection).Assembly.GetType(
            "Spectrum.Calibration.Correction.SpectrumCorrectionFeatureProvider"));
    }

    [Fact]
    public void MeasurementSnapshot_DefensivelyCopiesRelativeSpectrum()
    {
        double[] source = [0.1d, 0.5d, 1d];

        SpectrumMeasurementSnapshot snapshot = CreateSnapshot(source);
        source[0] = 99d;

        Assert.Equal([0.1d, 0.5d, 1d], snapshot.RelativeSpectrum);
        Assert.NotSame(source, snapshot.RelativeSpectrum);
    }

    [Fact]
    public void MeasurementSnapshot_ExposesReadOnlySpectrum()
    {
        SpectrumMeasurementSnapshot snapshot = CreateSnapshot([0.1d, 0.5d, 1d]);

        Assert.IsType<ReadOnlyCollection<double>>(snapshot.RelativeSpectrum);
        IList list = snapshot.RelativeSpectrum;
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list[0] = 99d);
    }

    [Fact]
    public void MeasurementSnapshot_NormalizesNullableTextAtContractBoundary()
    {
        SpectrumMeasurementSnapshot snapshot = new(
            resultId: 42,
            deviceCode: null!,
            serialNumber: null!,
            measuredAt: DateTimeOffset.UtcNow,
            startWavelength: 380,
            endWavelength: 380.2,
            interval: 0.1,
            relativeSpectrum: [0.1d, 0.5d, 1d],
            absoluteScale: 2,
            photometricValue: 100,
            integrationTime: 10,
            average: 1,
            measurementMode: null!,
            calibrationGroupName: null!,
            magnitudeFilePath: null!,
            magnitudeFileSha256: null!);

        Assert.Equal(string.Empty, snapshot.DeviceCode);
        Assert.Equal(string.Empty, snapshot.SerialNumber);
        Assert.Equal(string.Empty, snapshot.MeasurementMode);
        Assert.Equal(string.Empty, snapshot.CalibrationGroupName);
        Assert.Equal(string.Empty, snapshot.MagnitudeFilePath);
        Assert.Equal(string.Empty, snapshot.MagnitudeFileSha256);
    }

    [Fact]
    public void CorrectionHost_RequiresBothCallbacks()
    {
        Func<CancellationToken, Task<SpectrumMeasurementSnapshot>> capture =
            _ => Task.FromResult(CreateSnapshot([0.1d, 0.5d, 1d]));
        Func<SpectrumCorrectionApplyRequest, CancellationToken, Task<SpectrumCorrectionApplyResult>> apply =
            (request, _) => Task.FromResult(SpectrumCorrectionApplyResult.Success(request.MagnitudeFilePath));

        Assert.Throws<ArgumentNullException>(() => new SpectrumCorrectionHost(null!, apply));
        Assert.Throws<ArgumentNullException>(() => new SpectrumCorrectionHost(capture, null!));
        _ = new SpectrumCorrectionHost(capture, apply);
    }

    [Fact]
    public void ApplyResult_DistinguishesRestartRequestFromVerifiedSuccess()
    {
        SpectrumCorrectionApplyResult pending = SpectrumCorrectionApplyResult.PendingRestart("new.dat", "restart sent");

        Assert.Equal(SpectrumCorrectionApplyStatus.RestartRequested, pending.Status);
        Assert.True(pending.IsAccepted);
        Assert.False(pending.IsSuccess);
        Assert.Equal("new.dat", pending.AppliedMagnitudeFilePath);
    }

    private static SpectrumMeasurementSnapshot CreateSnapshot(IReadOnlyList<double> spectrum) =>
        new(
            resultId: 42,
            deviceCode: "spectrum-1",
            serialNumber: "SN001",
            measuredAt: DateTimeOffset.UtcNow,
            startWavelength: 380,
            endWavelength: 380.2,
            interval: 0.1,
            relativeSpectrum: spectrum,
            absoluteScale: 2,
            photometricValue: 100,
            integrationTime: 10,
            average: 1,
            measurementMode: "Luminance",
            calibrationGroupName: "Default",
            magnitudeFilePath: "Magiude.dat",
            magnitudeFileSha256: "ABCDEF");
}
