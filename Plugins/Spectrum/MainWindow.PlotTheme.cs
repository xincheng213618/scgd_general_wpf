using ColorVision.Themes;
using ScottPlot.WPF;

namespace Spectrum
{
    public partial class MainWindow
    {
        private ScottPlot.Color spectrumCurveColor = ScottPlot.Color.FromHex("#202020");

        private void ApplySpectrumPlotTheme(Theme theme)
        {
            bool isDark = theme == Theme.Dark;
            ScottPlot.Color background = ScottPlot.Color.FromHex(isDark ? "#0B0B0B" : "#FFFFFF");
            ScottPlot.Color foreground = ScottPlot.Color.FromHex(isDark ? "#F4F4F4" : "#202020");
            ScottPlot.Color border = ScottPlot.Color.FromHex(isDark ? "#383838" : "#D8D8D8");
            ScottPlot.Color minorGrid = ScottPlot.Color.FromHex(isDark ? "#242424" : "#ECECEC");
            spectrumCurveColor = foreground;

            Apply(wpfplot1);
            Apply(wpfplot2);

            void Apply(WpfPlot plotControl)
            {
                var plot = plotControl.Plot;
                plot.FigureBackground.Color = background;
                plot.DataBackground.Color = background;
                plot.Axes.Color(foreground);
                plot.Grid.MajorLineColor = border;
                plot.Grid.MinorLineColor = minorGrid;
                plot.Legend.BackgroundColor = background;
                plot.Legend.FontColor = foreground;
                plot.Legend.OutlineColor = border;
                if (!MulComparison)
                {
                    foreach (ScottPlot.Plottables.Scatter curve in plot.PlottableList.OfType<ScottPlot.Plottables.Scatter>())
                        curve.Color = spectrumCurveColor;
                }
                plotControl.Refresh();
            }
        }
    }
}
