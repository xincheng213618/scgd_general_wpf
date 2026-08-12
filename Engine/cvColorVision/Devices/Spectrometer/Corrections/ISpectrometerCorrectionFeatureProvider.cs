using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace cvColorVision;

public sealed record SpectrumCorrectionFeatureMetadata(
    string Id,
    string DisplayName,
    string Description,
    int Order = 0);

public sealed class SpectrumMeasurementSnapshot
{
    public long ResultId { get; }
    public string DeviceCode { get; }
    public string SerialNumber { get; }
    public DateTimeOffset MeasuredAt { get; }
    public double StartWavelength { get; }
    public double EndWavelength { get; }
    public double Interval { get; }
    public ReadOnlyCollection<double> RelativeSpectrum { get; }
    public double AbsoluteScale { get; }
    public double PhotometricValue { get; }
    public double IntegrationTime { get; }
    public int Average { get; }
    public string MeasurementMode { get; }
    public string CalibrationGroupName { get; }
    public string MagnitudeFilePath { get; }
    public string MagnitudeFileSha256 { get; }

    public SpectrumMeasurementSnapshot(
        long resultId,
        string deviceCode,
        string serialNumber,
        DateTimeOffset measuredAt,
        double startWavelength,
        double endWavelength,
        double interval,
        IReadOnlyList<double> relativeSpectrum,
        double absoluteScale,
        double photometricValue,
        double integrationTime,
        int average,
        string measurementMode,
        string calibrationGroupName,
        string magnitudeFilePath,
        string magnitudeFileSha256)
    {
        ArgumentNullException.ThrowIfNull(relativeSpectrum);

        ResultId = resultId;
        DeviceCode = deviceCode ?? string.Empty;
        SerialNumber = serialNumber ?? string.Empty;
        MeasuredAt = measuredAt;
        StartWavelength = startWavelength;
        EndWavelength = endWavelength;
        Interval = interval;
        RelativeSpectrum = Array.AsReadOnly(relativeSpectrum.ToArray());
        AbsoluteScale = absoluteScale;
        PhotometricValue = photometricValue;
        IntegrationTime = integrationTime;
        Average = average;
        MeasurementMode = measurementMode ?? string.Empty;
        CalibrationGroupName = calibrationGroupName ?? string.Empty;
        MagnitudeFilePath = magnitudeFilePath ?? string.Empty;
        MagnitudeFileSha256 = magnitudeFileSha256 ?? string.Empty;
    }
}

public sealed record SpectrumCorrectionApplyRequest(
    string MagnitudeFilePath,
    string CalibrationGroupName,
    string ExpectedSourceMagnitudeSha256);

public enum SpectrumCorrectionApplyStatus
{
    Succeeded,
    RestartRequested,
    Failed,
}

public sealed record SpectrumCorrectionApplyResult(
    SpectrumCorrectionApplyStatus Status,
    string Message,
    string AppliedMagnitudeFilePath)
{
    public bool IsSuccess => Status == SpectrumCorrectionApplyStatus.Succeeded;
    public bool IsAccepted => Status is SpectrumCorrectionApplyStatus.Succeeded or SpectrumCorrectionApplyStatus.RestartRequested;

    public static SpectrumCorrectionApplyResult Success(string path, string message = "") =>
        new(SpectrumCorrectionApplyStatus.Succeeded, message, path);

    public static SpectrumCorrectionApplyResult PendingRestart(string path, string message = "") =>
        new(SpectrumCorrectionApplyStatus.RestartRequested, message, path);

    public static SpectrumCorrectionApplyResult Failure(string message) =>
        new(SpectrumCorrectionApplyStatus.Failed, message, string.Empty);
}

public sealed class SpectrumCorrectionHost
{
    public Func<CancellationToken, Task<SpectrumMeasurementSnapshot>> CaptureAsync { get; }
    public Func<SpectrumCorrectionApplyRequest, CancellationToken, Task<SpectrumCorrectionApplyResult>> ApplyMagnitudeFileAsync { get; }

    public SpectrumCorrectionHost(
        Func<CancellationToken, Task<SpectrumMeasurementSnapshot>> captureAsync,
        Func<SpectrumCorrectionApplyRequest, CancellationToken, Task<SpectrumCorrectionApplyResult>> applyMagnitudeFileAsync)
    {
        CaptureAsync = captureAsync ?? throw new ArgumentNullException(nameof(captureAsync));
        ApplyMagnitudeFileAsync = applyMagnitudeFileAsync ?? throw new ArgumentNullException(nameof(applyMagnitudeFileAsync));
    }
}

public interface ISpectrometerCorrectionFeatureProvider
{
    SpectrumCorrectionFeatureMetadata Metadata { get; }

    Task ExecuteAsync(SpectrumCorrectionHost host, CancellationToken cancellationToken);
}
