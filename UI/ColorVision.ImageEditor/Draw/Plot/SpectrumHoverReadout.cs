using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColorVision.ImageEditor.Draw;

/// <summary>Transient cursor values and record context, without a permanent chart caption.</summary>
public sealed class SpectrumHoverReadout
{
    private readonly ToolTip tip = new()
    {
        Placement = PlacementMode.Relative, StaysOpen = true, IsHitTestVisible = false,
        MaxWidth = 560, Padding = new Thickness(12, 8, 12, 8)
    };
    private readonly TextBlock content = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };

    public SpectrumHoverReadout() => tip.Content = content;

    public void Hide() => tip.IsOpen = false;

    public void Show(FrameworkElement plot, SpectrumCursorReading? reading, SpectrumAnalysisSummary? summary, bool fluxMode, string record)
    {
        if (reading is not { } sample) { Hide(); return; }
        static string Number(double? value, string format) => value.HasValue && double.IsFinite(value.Value)
            ? value.Value.ToString(format, CultureInfo.CurrentCulture) : "—";
        var r9 = summary?.RenderingIndices.Count > 8 ? summary.RenderingIndices[8] : null;
        string intensity = fluxMode
            ? $"{DisplayText.Get("光通量")} {Number(summary?.LuminousFlux, "G6")} lm"
            : $"{DisplayText.Get("亮度")} {Number(summary?.Luminance, "G6")} cd/m²";
        content.Text = $"{record}\n{sample.Format(fluxMode ? "W/nm" : "W/(sr·m²·nm)")}\n"
            + $"CCT {Number(summary?.ColorTemperature, "0.0")} K    Duv {Number(summary?.Duv, "+0.00000;-0.00000;0.00000")}\n"
            + $"Ra {Number(summary?.Ra, "0.0")}    R9 {Number(r9, "0.0")}    {intensity}\n"
            + $"{DisplayText.Get("峰值波长")} {Number(summary?.PeakWavelength, "0.00")} nm    FWHM {Number(summary?.Fwhm, "0.00")} nm\n"
            + $"{DisplayText.Get("平均波长")} {Number(summary?.MeanWavelength, "0.00")} nm";
        var point = System.Windows.Input.Mouse.GetPosition(plot);
        tip.PlacementTarget = plot;
        tip.HorizontalOffset = point.X + 18;
        tip.VerticalOffset = point.Y + 18;
        tip.IsOpen = true;
    }
}
