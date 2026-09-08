using ColorVision.Themes;
using ScottPlot.WPF;
using System.Windows.Media;

namespace Spectrum
{
    public partial class MainWindow
    {
        private void ApplySpectrumPlotTheme(Theme theme)
        {
            bool isDark = theme == Theme.Dark;
            ScottPlot.Color background = GetSpectrumPlotColor("CV.Surface.Window", isDark ? "#262626" : "#FFFFFF");
            ScottPlot.Color surface = GetSpectrumPlotColor("CV.Surface.Control", isDark ? "#1C1C1C" : "#FFFFFF");
            ScottPlot.Color foreground = GetSpectrumPlotColor("CV.Text.Primary", isDark ? "#FFFFFF" : "#202020");
            ScottPlot.Color border = GetSpectrumPlotColor("CV.Border.Control", isDark ? "#3F3F46" : "#E5E5E5");
            ScottPlot.Color minorGrid = GetSpectrumPlotColor("CV.Border.Weak", isDark ? "#3F3F46" : "#E5E5E5");

            Apply(wpfplot1);
            Apply(wpfplot2);

            void Apply(WpfPlot plotControl)
            {
                var plot = plotControl.Plot;
                plot.FigureBackground.Color = background;
                plot.DataBackground.Color = surface;
                plot.Axes.Color(foreground);
                plot.Grid.MajorLineColor = border;
                plot.Grid.MinorLineColor = minorGrid;
                plot.Legend.BackgroundColor = background;
                plot.Legend.FontColor = foreground;
                plot.Legend.OutlineColor = border;
                plotControl.Refresh();
            }
        }

        private ScottPlot.Color GetSpectrumPlotColor(string resourceKey, string fallback)
        {
            if (TryFindResource(resourceKey) is SolidColorBrush brush)
            {
                var color = brush.Color;
                return new ScottPlot.Color(color.R, color.G, color.B, (byte)(color.A * brush.Opacity));
            }
            return ScottPlot.Color.FromHex(fallback);
        }
    }
}
