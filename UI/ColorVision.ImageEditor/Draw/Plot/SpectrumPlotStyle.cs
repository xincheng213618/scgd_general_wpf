using ScottPlot;
using ScottPlot.DataSources;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.ImageEditor.Draw;

/// <summary>Shared spectrum styling. Fill uses original samples, straight segments and a wavelength-axis gradient.</summary>
public static class SpectrumPlotStyle
{
    // A separate type lets live acquisition replace only its own fill layers.
    private sealed class SpectrumFill(Coordinates[] points) : Scatter(new ScatterSourceCoordinatesArray(points));

    public static void ClearFill(Plot plot)
    {
        foreach (var fill in plot.PlottableList.OfType<SpectrumFill>().ToArray()) plot.Remove(fill);
    }

    public static void AddFill(Plot plot, Scatter curve, Func<double, Color> wavelengthColor)
    {
        ClearFill(plot);
        List<Coordinates> run = new();
        // Clip each original segment to the visible band and y >= 0. Missing data breaks the path.
        var points = curve.Data.GetScatterPoints();
        for (int i = 1; i < points.Count; i++)
        {
            Coordinates a = points[i - 1], b = points[i];
            if (!double.IsFinite(a.X) || !double.IsFinite(b.X) || !double.IsFinite(a.Y) || !double.IsFinite(b.Y)
                || b.X <= a.X || b.X <= 380 || a.X >= 780 || (a.Y <= 0 && b.Y <= 0))
            { Flush(); continue; }
            double left = Math.Max(380, a.X), right = Math.Min(780, b.X);
            double y0 = left == a.X ? a.Y : a.Y + (b.Y - a.Y) * (left - a.X) / (b.X - a.X);
            double y1 = right == b.X ? b.Y : a.Y + (b.Y - a.Y) * (right - a.X) / (b.X - a.X);
            if (y0 < 0) { left += (right - left) * -y0 / (y1 - y0); y0 = 0; }
            if (y1 < 0) { right = left + (right - left) * y0 / (y0 - y1); y1 = 0; }
            if (right <= left) { Flush(); continue; }
            Coordinates start = new(left, y0), end = new(right, y1);
            if (run.Count > 0 && run[^1] != start) Flush();
            if (run.Count == 0) run.Add(start);
            run.Add(end);
        }
        Flush();

        void Flush()
        {
            if (run.Count >= 2)
            {
                SpectrumFill fill = new(run.ToArray()) { FillY = true, FillYValue = 0, LineWidth = 0, MarkerSize = 0 };
                for (int wavelength = 380; wavelength <= 780; wavelength += 2)
                    fill.ColorPositions.Add(new AxisGradientColorPosition(wavelengthColor(wavelength), wavelength));
                plot.Add.Plottable(fill);
            }
            run.Clear();
        }
    }

    public static Color ApplyTheme(Plot plot, bool dark)
    {
        Color background = Color.FromHex(dark ? "#15171B" : "#FAFAFA");
        Color foreground = Color.FromHex(dark ? "#F0F0F0" : "#303030");
        Color grid = Color.FromHex(dark ? "#363940" : "#DDDDDD");
        plot.FigureBackground.Color = background;
        plot.DataBackground.Color = background;
        plot.Axes.Color(foreground);
        plot.Grid.MajorLineColor = grid;
        plot.Grid.MinorLineColor = Color.FromHex(dark ? "#25282E" : "#EEEEEE");
        plot.Legend.BackgroundColor = background;
        plot.Legend.FontColor = foreground;
        plot.Legend.OutlineColor = grid;
        return foreground;
    }
}
