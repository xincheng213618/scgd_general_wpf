using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace Conoscope.Analysis
{
    public partial class ContrastResultWindow : Window
    {
        public ContrastResultWindow(ContrastComputationResult result)
        {
            InitializeComponent();
            ResultGrid.ItemsSource = result.Points.Select(item => new ContrastRowViewModel(item)).ToList();
            tbSummary.Text = $"共 {result.Points.Count} 个关注点，平均对比度 {result.AverageRatio:F3}:1，最小 {result.MinimumRatio:F3}:1，最大 {result.MaximumRatio:F3}:1";
        }

        private sealed class ContrastRowViewModel
        {
            public ContrastRowViewModel(ContrastPointResult source)
            {
                Source = source;
            }

            public ContrastPointResult Source { get; }
            public int Index => Source.Index;
            public string PointName => Source.PointName;
            public string AzimuthDisplay => FormatNullable(Source.AzimuthDegrees);
            public string PolarDisplay => FormatNullable(Source.PolarDegrees);
            public string RadiusDisplay => FormatNullable(Source.RadiusDegrees);
            public string WhiteDisplay => Source.White.Luminance.ToString("F4", CultureInfo.InvariantCulture);
            public string BlackDisplay => Source.Black.Luminance.ToString("F4", CultureInfo.InvariantCulture);
            public string RatioDisplay => Source.RatioText;
            public string WhiteChromaticityDisplay => FormatChromaticity(Source.White);
            public string BlackChromaticityDisplay => FormatChromaticity(Source.Black);

            private static string FormatNullable(double? value)
            {
                return value.HasValue ? value.Value.ToString("F2", CultureInfo.InvariantCulture) : "-";
            }

            private static string FormatChromaticity(ImageMeasurement measurement)
            {
                return $"x={measurement.Chromaticity.x:F4}, y={measurement.Chromaticity.y:F4}";
            }
        }
    }
}