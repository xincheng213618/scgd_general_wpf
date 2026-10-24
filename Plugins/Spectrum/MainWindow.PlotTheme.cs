using ColorVision.Themes;
using ScottPlot.WPF;

namespace Spectrum
{
    public partial class MainWindow
    {
        private ScottPlot.Color spectrumCurveColor = ScottPlot.Color.FromHex("#202020");

        private void ApplySpectrumPlotTheme(Theme theme)
        {
            Apply(wpfplot1);
            Apply(wpfplot2);

            void Apply(WpfPlot plotControl)
            {
                var plot = plotControl.Plot;
                spectrumCurveColor = ColorVision.ImageEditor.Draw.SpectrumPlotStyle.ApplyTheme(plot, theme == Theme.Dark);
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
