using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WPF;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw;

public readonly record struct SpectrumCursorReading(int SampleIndex, double Wavelength, float Relative, double Absolute)
{
    public string Format(string absoluteUnit)
    {
        static string Number(double value) => double.IsFinite(value) ? value.ToString("G7", CultureInfo.CurrentCulture) : "—";
        return $"{Wavelength:0.######} nm   {DisplayText.Get("相对值")} {Number(Relative)}   {DisplayText.Get("绝对值")} {Number(Absolute)} {absoluteUnit}";
    }
}

public static class SpectrumCursorSamples
{
    public static bool TryRead(IReadOnlyList<float> samples, float start, float interval, float scale, double wavelength, out SpectrumCursorReading reading)
    {
        reading = default;
        if (samples.Count == 0 || !float.IsFinite(start) || start <= 0 || !float.IsFinite(interval) || interval <= 0 || !double.IsFinite(wavelength)) return false;
        double first = double.Parse(start.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        double step = double.Parse(interval.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        double position = (wavelength - first) / step;
        if (position < -1e-8 || position > samples.Count - 1 + 1e-8) return false;
        int index = Math.Clamp((int)Math.Round(position, MidpointRounding.AwayFromZero), 0, samples.Count - 1);
        float relative = samples[index];
        reading = new(index, first + step * index, relative, (double)relative * scale);
        return true;
    }
}

/// <summary>Selected-record cursor, independent of display decimation and gradient-fill plottables.</summary>
public sealed class SpectrumPlotCursor : IDisposable
{
    private readonly WpfPlot control;
    private readonly Action<SpectrumCursorReading?> changed;
    private float[] samples = Array.Empty<float>();
    private float start, interval, scale;
    private readonly bool absolute;
    private VerticalLine? line;
    private Marker? marker;
    private bool disposed;

    public SpectrumPlotCursor(WpfPlot control, bool absolute, Action<SpectrumCursorReading?> changed)
    {
        this.control = control;
        this.absolute = absolute;
        this.changed = changed;
        control.MouseMove += MouseMove;
        control.MouseLeave += MouseLeave;
    }

    public void SetSpectrum(float[]? values, float startWavelength, float sampleInterval, float absoluteScale)
    {
        Clear();
        samples = values == null ? Array.Empty<float>() : (float[])values.Clone();
        start = startWavelength; interval = sampleInterval; scale = absoluteScale;
    }

    private void MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed) { Hide(); return; }
        var point = e.GetPosition(control);
        var dpi = VisualTreeHelper.GetDpi(control);
        UpdateAtPixel(new Pixel(point.X * dpi.DpiScaleX, point.Y * dpi.DpiScaleY));
    }

    public void UpdateAtPixel(Pixel pixel)
    {
        if (disposed) return;
        var rect = control.Plot.LastRender.DataRect;
        if (pixel.X < rect.Left || pixel.X > rect.Right || pixel.Y < rect.Top || pixel.Y > rect.Bottom
            || !SpectrumCursorSamples.TryRead(samples, start, interval, scale, control.Plot.GetCoordinates(pixel).X, out var reading))
        { Hide(); return; }
        if (line == null || !control.Plot.PlottableList.Contains(line))
            line = control.Plot.Add.VerticalLine(reading.Wavelength, 1, ScottPlot.Colors.Gray, LinePattern.Dashed);
        if (marker == null || !control.Plot.PlottableList.Contains(marker))
            marker = control.Plot.Add.Marker(reading.Wavelength, 0, MarkerShape.FilledCircle, 7, ScottPlot.Colors.Orange);
        line.Position = reading.Wavelength;
        line.IsVisible = true;
        double y = absolute ? reading.Absolute : reading.Relative;
        marker.X = reading.Wavelength; marker.Y = y; marker.IsVisible = double.IsFinite(y);
        changed(reading);
        control.Refresh();
    }

    private void MouseLeave(object sender, MouseEventArgs e) => Hide();
    private void Hide()
    {
        bool visible = line?.IsVisible == true || marker?.IsVisible == true;
        if (line != null) line.IsVisible = false;
        if (marker != null) marker.IsVisible = false;
        changed(null);
        if (visible) control.Refresh();
    }

    public void Clear()
    {
        samples = Array.Empty<float>();
        if (line != null) control.Plot.Remove(line);
        if (marker != null) control.Plot.Remove(marker);
        line = null; marker = null;
        changed(null);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        control.MouseMove -= MouseMove;
        control.MouseLeave -= MouseLeave;
        Clear();
    }
}
