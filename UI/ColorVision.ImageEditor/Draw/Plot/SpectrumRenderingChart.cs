using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw;

/// <summary>R1–R15 bars with a signed axis. Missing values have no bar and are labelled with a dash.</summary>
public sealed class SpectrumRenderingChart : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(nameof(Values), typeof(IReadOnlyList<double?>), typeof(SpectrumRenderingChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(SpectrumRenderingChart), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));
    public IReadOnlyList<double?>? Values { get => (IReadOnlyList<double?>?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 100 || ActualHeight < 80) return;
        double?[] values = Enumerable.Range(0, 15).Select(i => Values != null && i < Values.Count && Values[i] is double v && double.IsFinite(v) ? (double?)v : null).ToArray();
        double minimum = Math.Min(0, values.Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(0).Min());
        double maximum = Math.Max(100, values.Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(100).Max());
        if (minimum < 0) minimum = Math.Floor(minimum / 20) * 20 - 20;
        double rawStep = (maximum - minimum) / 5;
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        double scaledStep = rawStep / magnitude;
        double step = (scaledStep <= 1 ? 1 : scaledStep <= 2 ? 2 : scaledStep <= 5 ? 5 : 10) * magnitude;
        minimum = Math.Floor(minimum / step) * step;
        maximum = Math.Ceiling(maximum / step) * step;
        double left = 42, top = 24, width = ActualWidth - left - 12, height = ActualHeight - top - 30;
        double Y(double value) => top + (maximum - value) / (maximum - minimum) * height;
        Brush gridBrush = Foreground.CloneCurrentValue();
        gridBrush.Opacity = 0.15;
        var grid = new Pen(gridBrush, 1);
        for (double value = minimum; value <= maximum + step * 1e-8; value += step)
        {
            double y = Y(value);
            dc.DrawLine(grid, new Point(left, y), new Point(left + width, y));
            Text(value.ToString("0.#", CultureInfo.CurrentCulture), left - 7, y - 7, true);
        }
        dc.DrawLine(new Pen(Foreground, 1), new Point(left, Y(0)), new Point(left + width, Y(0)));
        double spacing = width / 15;
        for (int i = 0; i < 15; i++)
        {
            double center = left + (i + 0.5) * spacing;
            Text($"R{i + 1}", center, top + height + 8);
            if (values[i] is not double value) { Text("—", center, Y(0) - 18); continue; }
            double y = Y(value), zero = Y(0);
            var brush = new SolidColorBrush(i == 8 ? Color.FromRgb(207, 91, 86) : Color.FromRgb(76, 133, 213));
            dc.DrawRectangle(brush, null, new Rect(center - spacing * 0.3, Math.Min(y, zero), spacing * 0.6, Math.Max(0.5, Math.Abs(zero - y))));
            Text(value.ToString("0.#", CultureInfo.CurrentCulture), center, value >= 0 ? y - 20 : y + 3);
        }

        void Text(string value, double x, double y, bool alignRight = false)
        {
            FormattedText text = new(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(text, new Point(x - (alignRight ? text.Width : text.Width / 2), y));
        }
    }
}
