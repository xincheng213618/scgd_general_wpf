using ColorVision.Themes;
using ScottPlot;
using System;
using System.Linq;
using System.Windows;

namespace ColorVision.ImageEditor.Draw;

/// <summary>Offline analysis of one captured record, independent of later list selection or acquisition.</summary>
public partial class SpectrumAnalysisWindow : Window
{
    private readonly SpectrumAnalysisSummary summary;
    private readonly float[] samples;
    private readonly float start, interval, scale;
    private readonly bool fluxMode;
    private readonly string description;
    private readonly Func<double, Color> wavelengthColor;
    private readonly SpectrumHoverReadout readout = new();
    private SpectrumPlotCursor? cursor;

    public SpectrumAnalysisWindow(SpectrumAnalysisSummary summary, float[] samples, float start, float interval,
        float scale, bool fluxMode, string description, Func<double, Color> wavelengthColor)
    {
        this.summary = summary with { RenderingIndices = summary.RenderingIndices.ToArray() };
        this.samples = (float[])samples.Clone();
        this.start = start; this.interval = interval; this.scale = scale;
        this.fluxMode = fluxMode; this.description = description; this.wavelengthColor = wavelengthColor;
        InitializeComponent();
        Title = $"{DisplayText.Get("光谱分析")} · {description}";
        RecordDescription.Text = description;
        Analysis.Value = this.summary;
        Analysis.IsFluxMode = fluxMode;
        Analysis.RenderingExpander.IsExpanded = true;
        Loaded += Window_Loaded;
        Closed += Window_Closed;
        ThemeManager.Current.CurrentUIThemeChanged += ApplyTheme;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => Draw();
    private void SpectrumType_Click(object sender, RoutedEventArgs e) => Draw();

    private void Draw()
    {
        cursor?.Dispose();
        SpectrumPlot.Plot.Clear();
        bool absolute = AbsoluteToggle.IsChecked == true;
        string unit = fluxMode ? "W/nm" : "W/(sr·m²·nm)";
        SpectrumPlot.Plot.XLabel(DisplayText.Get("波长") + " (nm)");
        SpectrumPlot.Plot.YLabel(absolute ? DisplayText.Get("绝对光谱") + $" ({unit})" : DisplayText.Get("相对光谱"));
        SpectrumPlot.Plot.Axes.Bottom.Label.FontName = Fonts.Detect("波长");
        SpectrumPlot.Plot.Axes.Left.Label.FontName = Fonts.Detect("相对光谱");
        bool valid = samples.Any(float.IsFinite) && float.IsFinite(start) && start > 0
            && float.IsFinite(interval) && interval > 0 && (!absolute || float.IsFinite(scale));
        NoSpectrum.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
        if (!valid)
        {
            SpectrumPlot.Plot.Axes.SetLimits(380, 780, 0, 1);
            SpectrumPlotStyle.ApplyTheme(SpectrumPlot.Plot, ThemeManager.Current.CurrentUITheme == Theme.Dark);
            SpectrumPlot.Refresh();
            return;
        }
        var xs = new double[samples.Length];
        var ys = new double[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            xs[i] = start + (double)interval * i;
            ys[i] = absolute ? (double)samples[i] * scale : samples[i];
        }
        var curve = SpectrumPlot.Plot.Add.Scatter(xs, ys);
        curve.LineWidth = 1.5f; curve.MarkerSize = 0;
        SpectrumPlotStyle.AddFill(SpectrumPlot.Plot, curve, wavelengthColor);
        SpectrumPlot.Plot.Axes.AutoScale();
        double end = start + (double)interval * (samples.Length - 1);
        if (double.IsFinite(end) && end > start) SpectrumPlot.Plot.Axes.SetLimitsX(start, end);
        curve.Color = SpectrumPlotStyle.ApplyTheme(SpectrumPlot.Plot, ThemeManager.Current.CurrentUITheme == Theme.Dark);
        cursor = new(SpectrumPlot, absolute, reading => readout.Show(SpectrumPlot, reading, summary, fluxMode, description));
        cursor.SetSpectrum(samples, start, interval, scale);
        SpectrumPlot.Refresh();
    }

    private void ApplyTheme(Theme theme)
    {
        if (!Dispatcher.CheckAccess()) { _ = Dispatcher.BeginInvoke(() => ApplyTheme(theme)); return; }
        var color = SpectrumPlotStyle.ApplyTheme(SpectrumPlot.Plot, theme == Theme.Dark);
        foreach (var curve in SpectrumPlot.Plot.PlottableList.OfType<ScottPlot.Plottables.Scatter>())
            if (curve.LineWidth > 0) curve.Color = color;
        SpectrumPlot.Refresh();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        ThemeManager.Current.CurrentUIThemeChanged -= ApplyTheme;
        cursor?.Dispose();
        readout.Hide();
    }
}
