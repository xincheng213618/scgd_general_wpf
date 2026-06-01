using ColorVision.ImageEditor.Cie;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Conoscope.Analysis
{
    public partial class ColorGamutResultWindow : Window
    {
        private readonly ColorGamutComputationResult result;
        private readonly List<ColorGamutRowViewModel> rows;
        private bool isSyncingSelection;

        public ColorGamutResultWindow(ColorGamutComputationResult result)
        {
            InitializeComponent();
            this.result = result;
            rows = result.Points.Select(item => new ColorGamutRowViewModel(item)).ToList();

            tbStandardName.Text = result.Standard.Name;
            tbSummary.Text = Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.FocusPointCountAndAverageGamut, rows.Count, result.AverageCoveragePercent.ToString("F2"));

            ResultGrid.ItemsSource = rows;
            cbDisplayScope.ItemsSource = CreateScopeOptions();
            cbDisplayScope.SelectedIndex = 0;

            CieDiagram.SetDiagram(CieDiagramKind.Cie1931xy);
            CieDiagram.ShowCctReference = false;
            CieDiagram.ShowDaylightReference = false;
            RenderDiagram();
        }

        private List<DisplayScopeOption> CreateScopeOptions()
        {
            List<DisplayScopeOption> options = new()
            {
                new DisplayScopeOption(string.Empty, Properties.Resources.Conoscope_AllFocusPoints)
            };

            options.AddRange(rows.Select(row => new DisplayScopeOption(row.PointKey, $"{row.Index}. {row.PointName}")));
            return options;
        }

        private void cbDisplayScope_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isSyncingSelection)
            {
                return;
            }

            RenderDiagram();
        }

        private void ResultGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ResultGrid.SelectedItem is not ColorGamutRowViewModel row)
            {
                return;
            }

            DisplayScopeOption? option = ((IEnumerable<DisplayScopeOption>)cbDisplayScope.ItemsSource).FirstOrDefault(item => item.Key == row.PointKey);
            if (option == null)
            {
                return;
            }

            isSyncingSelection = true;
            cbDisplayScope.SelectedItem = option;
            isSyncingSelection = false;
            RenderDiagram();
        }

        private void btnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            AnalysisResultCsvExporter.ExportColorGamut(this, result);
        }

        private void RenderDiagram()
        {
            IReadOnlyList<ColorGamutPointResult> pointsToRender = ResolvePointsToRender();
            List<CieGamut> gamuts = new()
            {
                CreateStandardGamut(result.Standard)
            };

            List<CieMarker> markers = new();
            foreach ((ColorGamutPointResult pointResult, int index) in pointsToRender.Select((item, idx) => (item, idx)))
            {
                Color accent = CreateAccentColor(index);
                gamuts.Add(CreateMeasuredGamut(pointResult, accent));
                markers.Add(new CieMarker($"{pointResult.PointName}-R", pointResult.RedChromaticity, Color.FromRgb(222, 71, 64)));
                markers.Add(new CieMarker($"{pointResult.PointName}-G", pointResult.GreenChromaticity, Color.FromRgb(56, 166, 82)));
                markers.Add(new CieMarker($"{pointResult.PointName}-B", pointResult.BlueChromaticity, Color.FromRgb(60, 123, 246)));
            }

            CieDiagram.SetGamuts(gamuts);
            CieDiagram.SetMarkers(markers);
            CieDiagram.SetReferenceMarkers(Array.Empty<CieMarker>());
            CieDiagram.ClearSelection();
            CieDiagram.ZoomUniform();
        }

        private IReadOnlyList<ColorGamutPointResult> ResolvePointsToRender()
        {
            if (cbDisplayScope.SelectedItem is not DisplayScopeOption option || string.IsNullOrWhiteSpace(option.Key))
            {
                return result.Points;
            }

            return result.Points.Where(item => item.PointKey == option.Key).ToArray();
        }

        private static CieGamut CreateStandardGamut(ColorGamutStandard standard)
        {
            CieGamut? defaultGamut = CieGamuts.Defaults.FirstOrDefault(item => string.Equals(item.Name, standard.Name, StringComparison.Ordinal));
            if (defaultGamut != null)
            {
                return defaultGamut;
            }

            return new CieGamut(
                standard.Name,
                new[]
                {
                    new CieChromaticity(standard.Red.X, standard.Red.Y),
                    new CieChromaticity(standard.Green.X, standard.Green.Y),
                    new CieChromaticity(standard.Blue.X, standard.Blue.Y)
                },
                Brushes.DimGray,
                new SolidColorBrush(Color.FromArgb(22, 64, 64, 64)));
        }

        private static CieGamut CreateMeasuredGamut(ColorGamutPointResult pointResult, Color accentColor)
        {
            SolidColorBrush stroke = new(accentColor);
            SolidColorBrush fill = new(Color.FromArgb(28, accentColor.R, accentColor.G, accentColor.B));
            return new CieGamut(
                Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.Measured, pointResult.PointName),                new[]
                {
                    pointResult.RedChromaticity,
                    pointResult.GreenChromaticity,
                    pointResult.BlueChromaticity
                },
                stroke,
                fill);
        }

        private static Color CreateAccentColor(int index)
        {
            Color[] colors =
            {
                Color.FromRgb(255, 140, 66),
                Color.FromRgb(99, 102, 241),
                Color.FromRgb(16, 185, 129),
                Color.FromRgb(236, 72, 153),
                Color.FromRgb(234, 179, 8),
                Color.FromRgb(20, 184, 166),
            };

            return colors[index % colors.Length];
        }

        private sealed record DisplayScopeOption(string Key, string Label)
        {
            public override string ToString() => Label;
        }

        private sealed class ColorGamutRowViewModel
        {
            public ColorGamutRowViewModel(ColorGamutPointResult source)
            {
                Source = source;
            }

            public ColorGamutPointResult Source { get; }
            public int Index => Source.Index;
            public string PointKey => Source.PointKey;
            public string PointName => Source.PointName;
            public string AzimuthDisplay => FormatNullable(Source.AzimuthDegrees);
            public string PolarDisplay => FormatNullable(Source.PolarDegrees);
            public string RadiusDisplay => FormatNullable(Source.RadiusDegrees);
            public string RedDisplay => FormatChromaticity(Source.Red);
            public string GreenDisplay => FormatChromaticity(Source.Green);
            public string BlueDisplay => FormatChromaticity(Source.Blue);
            public string CoverageDisplay => Source.CoveragePercent.ToString("F2", CultureInfo.InvariantCulture);

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
