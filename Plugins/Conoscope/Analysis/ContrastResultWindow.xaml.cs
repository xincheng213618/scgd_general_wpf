using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace Conoscope.Analysis
{
    public partial class ContrastResultWindow : Window
    {
        private readonly ContrastComputationResult result;

        public ContrastResultWindow(ContrastComputationResult result)
        {
            InitializeComponent();
            this.result = result;
            ResultGrid.ItemsSource = result.Points.Select(item => new ContrastRowViewModel(item)).ToList();
            tbSummary.Text = Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.FocusPointCountAndAverageContrast, result.Points.Count, result.AverageRatio.ToString("F3"), result.MinimumRatio.ToString("F3"), result.MaximumRatio.ToString("F3"));
        }

        private void btnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            AnalysisResultCsvExporter.ExportContrast(this, result);
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
                return Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.ChromaticityFormat, measurement.Chromaticity.x.ToString("F4"), measurement.Chromaticity.y.ToString("F4"));
            }
        }
    }
}
