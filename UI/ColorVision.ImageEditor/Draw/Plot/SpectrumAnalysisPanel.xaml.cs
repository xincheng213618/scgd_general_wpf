using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.Draw;

public partial class SpectrumAnalysisPanel : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(SpectrumAnalysisSummary), typeof(SpectrumAnalysisPanel), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty IsFluxModeProperty = DependencyProperty.Register(nameof(IsFluxMode), typeof(bool), typeof(SpectrumAnalysisPanel), new PropertyMetadata(false, Changed));
    public SpectrumAnalysisSummary? Value { get => (SpectrumAnalysisSummary?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool IsFluxMode { get => (bool)GetValue(IsFluxModeProperty); set => SetValue(IsFluxModeProperty, value); }

    public SpectrumAnalysisPanel()
    {
        InitializeComponent();
        Rebuild();
    }

    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((SpectrumAnalysisPanel)sender).Rebuild();
    private void Rebuild()
    {
        if (SummaryMetrics == null) return;
        var value = Value;
        SummaryMetrics.ItemsSource = new[]
        {
            Metric("CCT", value?.ColorTemperature, "0.0", "K", value?.TemperatureStatus),
            Metric("Duv", value?.Duv, "+0.00000;-0.00000;0.00000", "", value?.TemperatureStatus),
            Metric("Ra", value?.Ra, "0.0", "", value?.RenderingStatus),
            Metric("R9", value?.RenderingIndices.Count > 8 ? value.RenderingIndices[8] : null, "0.0", "", value?.RenderingStatus),
            Metric(IsFluxMode ? DisplayText.Get("光通量") : DisplayText.Get("亮度"), IsFluxMode ? value?.LuminousFlux : value?.Luminance, "G6", IsFluxMode ? "lm" : "cd/m²"),
            Metric(DisplayText.Get("峰值波长"), value?.PeakWavelength, "0.00", "nm"),
            Metric("FWHM", value?.Fwhm, "0.00", "nm"),
            Metric(DisplayText.Get("平均波长"), value?.MeanWavelength, "0.00", "nm")
        };
        bool available = value?.RenderingIndices.Any(index => index.HasValue && double.IsFinite(index.Value)) == true;
        RenderingChart.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        RenderingUnavailable.Visibility = available ? Visibility.Collapsed : Visibility.Visible;
    }

    private static SummaryMetric Metric(string label, double? value, string format, string unit, string? explanation = null)
        => new(label, value.HasValue && double.IsFinite(value.Value) ? value.Value.ToString(format, CultureInfo.CurrentCulture) : "—", unit, explanation ?? label);

    private sealed record SummaryMetric(string Label, string Value, string Unit, string Explanation);
}
