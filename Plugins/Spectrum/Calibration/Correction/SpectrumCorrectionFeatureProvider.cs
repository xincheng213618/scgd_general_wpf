using cvColorVision;
using System.Windows;
using System.Windows.Threading;

namespace Spectrum.Calibration.Correction;

/// <summary>
/// Reflection-discovered correction entry point. It opens only the correction window;
/// all acquisition and application operations are delegated back to the service host.
/// </summary>
public sealed class SpectrumCorrectionFeatureProvider : ISpectrometerCorrectionFeatureProvider
{
    public SpectrumCorrectionFeatureMetadata Metadata { get; } = new(
        "spectrum.service-result-correction",
        "光谱修正",
        "使用服务实测数据进行完整光谱或单独亮度修正",
        10);

    public async Task ExecuteAsync(SpectrumCorrectionHost host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        Dispatcher dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("当前没有可用的 WPF Dispatcher。");

        if (dispatcher.CheckAccess())
        {
            ShowWindow(host, cancellationToken);
            return;
        }

        await dispatcher.InvokeAsync(
            () => ShowWindow(host, cancellationToken),
            DispatcherPriority.Normal,
            cancellationToken);
    }

    private static void ShowWindow(SpectrumCorrectionHost host, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Window? owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
        var window = new SpectrumCorrectionWindow(host, cancellationToken);
        if (owner != null && owner != window)
            window.Owner = owner;
        window.ShowDialog();
    }
}
