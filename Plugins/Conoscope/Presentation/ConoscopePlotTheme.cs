using ColorVision.Themes;
using Conoscope.Core;
using ScottPlot.WPF;
using System.Windows;
using System.Windows.Media;

namespace Conoscope.Presentation
{
    /// <summary>Shared presentation colors for the Cartesian and polar reference charts.</summary>
    internal sealed class ConoscopePlotTheme
    {
        public SolidColorBrush Background { get; }
        public SolidColorBrush Foreground { get; }
        public SolidColorBrush MutedForeground { get; }
        public SolidColorBrush Grid { get; }
        public SolidColorBrush MinorGrid { get; }

        private ConoscopePlotTheme(FrameworkElement owner)
        {
            bool isDark = ThemeManager.Current.CurrentUITheme == Theme.Dark;
            Background = ResolveBrush(owner, "CV.Surface.Window", isDark ? "#262626" : "#FFFFFF");
            Foreground = ResolveBrush(owner, "CV.Text.Primary", isDark ? "#F1F3F5" : "#20252B");
            MutedForeground = ResolveBrush(owner, "CV.Text.Secondary", isDark ? "#B8BEC7" : "#5F6875");
            Grid = ResolveBrush(owner, "CV.Border.Control", isDark ? "#3F3F46" : "#E5E5E5");
            MinorGrid = ResolveBrush(owner, "CV.Border.Weak", isDark ? "#333338" : "#F0F0F0");
        }

        public static ConoscopePlotTheme Resolve(FrameworkElement owner) => new(owner);

        public static void ApplyCartesian(WpfPlot plotControl, FrameworkElement owner)
        {
            ConoscopePlotTheme theme = Resolve(owner);
            ScottPlot.Plot plot = plotControl.Plot;
            plot.FigureBackground.Color = ToPlotColor(theme.Background);
            plot.DataBackground.Color = ToPlotColor(theme.Background);
            plot.Axes.Color(ToPlotColor(theme.MutedForeground));
            plot.Axes.Title.Label.ForeColor = ToPlotColor(theme.Foreground);
            plot.Axes.Title.Label.FontSize = 14;
            plot.Axes.Left.Label.FontSize = 12;
            plot.Axes.Bottom.Label.FontSize = 12;
            plot.Axes.Left.TickLabelStyle.FontSize = 11;
            plot.Axes.Bottom.TickLabelStyle.FontSize = 11;
            plot.Grid.MajorLineColor = ToPlotColor(theme.Grid);
            plot.Grid.MajorLineWidth = 0.75f;
            plot.Grid.MinorLineColor = ToPlotColor(theme.MinorGrid);
            plot.Legend.BackgroundColor = ToPlotColor(theme.Background);
            plot.Legend.FontColor = ToPlotColor(theme.MutedForeground);
            plot.Legend.OutlineColor = ToPlotColor(theme.Grid);
            plot.Legend.FontSize = 11;
            plotControl.Refresh();
        }

        public static SolidColorBrush GetChannelBrush(ExportChannel channel, FrameworkElement owner)
        {
            Color background = Resolve(owner).Background.Color;
            bool isDark = background.R * 0.2126 + background.G * 0.7152 + background.B * 0.0722 < 128;
            // Keep each channel's established color family, with contrast suitable for its surface.
            string color = channel switch
            {
                ExportChannel.X => isDark ? "#E3B65D" : "#A67116",
                ExportChannel.Y => isDark ? "#6DCAAA" : "#23856C",
                ExportChannel.Z => isDark ? "#C69DE4" : "#9466B4",
                ExportChannel.CieX => isDark ? "#EB967D" : "#BB5C3D",
                ExportChannel.CieY => isDark ? "#8CC49C" : "#397F52",
                ExportChannel.CieU => isDark ? "#80B7EE" : "#3D7CBB",
                ExportChannel.CieV => isDark ? "#ACA0E6" : "#7B64B3",
                ExportChannel.ColorDifference => isDark ? "#E994A7" : "#BD4A69",
                ExportChannel.Contrast => isDark ? "#6AC6DD" : "#25869D",
                _ => isDark ? "#6DCAAA" : "#23856C"
            };
            return CreateBrush((Color)ColorConverter.ConvertFromString(color));
        }

        private static SolidColorBrush ResolveBrush(FrameworkElement owner, string key, string fallback)
        {
            if (owner.TryFindResource(key) is SolidColorBrush resource)
            {
                Color color = resource.Color;
                color.A = (byte)(color.A * resource.Opacity);
                return CreateBrush(color);
            }
            return CreateBrush((Color)ColorConverter.ConvertFromString(fallback));
        }

        private static SolidColorBrush CreateBrush(Color color)
        {
            SolidColorBrush brush = new(color);
            brush.Freeze();
            return brush;
        }

        private static ScottPlot.Color ToPlotColor(SolidColorBrush brush)
        {
            Color color = brush.Color;
            return new ScottPlot.Color(color.R, color.G, color.B, color.A);
        }
    }
}
