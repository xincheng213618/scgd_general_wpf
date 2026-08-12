using System;
using System.Threading;
using System.Threading.Tasks;
namespace ColorVision.Engine.Services.Devices.Spectrum.Correction;

/// <summary>
/// Supplies the Engine correction window with service acquisition and apply operations.
/// </summary>
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
