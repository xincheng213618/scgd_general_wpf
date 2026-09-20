using ColorVision.Core;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;

public partial class SfrSimplePlotControl : UserControl
{
    private SfrAnalysisResult? _result;
    private int _view;
    private bool _dark;
    public void SetDarkTheme(bool dark)
    {
        _dark = dark;
        var plot = WpfPlot.Plot;
        var background = ScottPlot.Color.FromHex(dark ? "#252930" : "#FFFFFF");
        var foreground = ScottPlot.Color.FromHex(dark ? "#E5E7EB" : "#252525");
        plot.FigureBackground.Color = background;
        plot.DataBackground.Color = ScottPlot.Color.FromHex(dark ? "#1C2026" : "#FAFBFC");
        plot.Axes.Color(foreground);
        plot.Grid.MajorLineColor = ScottPlot.Color.FromHex(dark ? "#414750" : "#D8DDE5");
        plot.Legend.BackgroundColor = background; plot.Legend.FontColor = foreground;
        plot.Legend.OutlineColor = ScottPlot.Color.FromHex(dark ? "#555C68" : "#CDD3DC");
    }
    private HashSet<string> _visible = [];
    public event Action<string>? CursorReadout;

    public SfrSimplePlotControl()
    {
        InitializeComponent();
        WpfPlot.MouseMove += (_, e) =>
        {
            if (_result == null || _view != 0) return;
            var point = e.GetPosition(WpfPlot);
            var dpi = VisualTreeHelper.GetDpi(WpfPlot);
            double frequency = WpfPlot.Plot.GetCoordinates(new Pixel(point.X * dpi.DpiScaleX, point.Y * dpi.DpiScaleY)).X;
            if (frequency < 0 || frequency > 0.5) return;
            CursorReadout?.Invoke($"游标 {frequency:F4} cy/pixel  " + string.Join("   ", _result.Channels.Where(c => c.Valid && _visible.Contains(c.Channel))
                .Select(c => $"{c.Channel}: {SfrCurveQueries.AtFrequency(c.Frequencies, c.Mtf, frequency):P1}")));
        };
    }

    public void ShowResult(SfrAnalysisResult result, int view, HashSet<string> visible, bool extended)
    {
        _result = result;
        _view = view;
        _visible = visible;
        var plot = WpfPlot.Plot;
        plot.Clear();
        var allY = new List<double>();
        foreach (var channel in result.Channels.Where(c => c.Valid && visible.Contains(c.Channel)))
        {
            double[] x = view == 1 ? channel.EdgePositions : view == 2 ? channel.LsfPositions : channel.Frequencies;
            double[] y = view == 1 ? channel.Esf : view == 2 ? channel.Lsf : channel.Mtf;
            var scatter = plot.Add.Scatter(x, y);
            scatter.LegendText = channel.Channel == "L" ? "L · 组合信号" : channel.Channel;
            scatter.Color = ChannelColor(channel.Channel);
            scatter.LineWidth = channel.Channel == "L" ? 2.5f : 1.5f;
            scatter.MarkerSize = 0;
            scatter.LinePattern = channel.Channel == "R" ? LinePattern.Dashed : channel.Channel == "B" ? LinePattern.Dotted : LinePattern.Solid;
            for (int i = 0; i < x.Length; i++)
                if (view == 0 ? x[i] <= (extended ? 1 : 0.5) : Math.Abs(x[i]) <= 12) allY.Add(y[i]);
            if (view == 0)
            {
                foreach (double threshold in new[] { 0.5, 0.1 })
                {
                    double? f = SfrCurveQueries.Crossing(x, y, threshold);
                    if (f.HasValue)
                    {
                        var mark = plot.Add.Scatter(new[] { f.Value }, new[] { threshold });
                        mark.Color = scatter.Color;
                        mark.MarkerSize = 7;
                        mark.LineWidth = 0;
                    }
                }
            }
        }
        if (view == 0)
        {
            plot.Add.HorizontalLine(0.5, 1, ScottPlot.Colors.Gray, LinePattern.Dashed);
            plot.Add.HorizontalLine(0.1, 1, ScottPlot.Colors.Gray, LinePattern.Dotted);
            plot.Add.VerticalLine(0.5, 1, ScottPlot.Colors.Gray, LinePattern.Dashed);
            plot.Axes.Bottom.Label.Text = "空间频率 (cycles/pixel)";
            plot.Axes.Left.Label.Text = "MTF · 相对响应";
            plot.Axes.SetLimits(0, extended ? 1 : 0.5, 0, Math.Max(1.05, allY.DefaultIfEmpty(1).Max() * 1.08));
        }
        else
        {
            plot.Axes.Bottom.Label.Text = "边缘法线方向位置 (pixel)";
            plot.Axes.Left.Label.Text = view == 1 ? "ESF · 归一化边缘信号" : "LSF · 响应 / pixel";
            double low = Math.Min(0, allY.DefaultIfEmpty(0).Min());
            double high = Math.Max(view == 1 ? 1 : 0.1, allY.DefaultIfEmpty(1).Max());
            plot.Axes.SetLimits(-12, 12, low - (high - low) * 0.05, high + (high - low) * 0.08);
        }
        string font = ScottPlot.Fonts.Detect("斜边清晰度");
        plot.Axes.Left.Label.FontName = font;
        plot.Axes.Bottom.Label.FontName = font;
        plot.ShowLegend();
        plot.Legend.Alignment = Alignment.UpperRight;
        plot.Legend.FontName = font;
        WpfPlot.Refresh();
    }

    public void Clear()
    {
        _result = null;
        WpfPlot.Plot.Clear();
        WpfPlot.Refresh();
    }

    private ScottPlot.Color ChannelColor(string channel) => ScottPlot.Color.FromHex(channel switch
    {
        "R" => _dark ? "#FF8585" : "#BD3535", "G" => _dark ? "#76E396" : "#20833C", "B" => _dark ? "#89B8FF" : "#3267CD", _ => _dark ? "#FFD077" : "#876000"
    });
}
