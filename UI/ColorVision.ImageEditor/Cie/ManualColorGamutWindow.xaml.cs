using ColorVision.Themes;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Cie
{
    public partial class ManualColorGamutWindow : Window
    {
        private readonly List<StandardOption> standardOptions = new();
        private readonly ObservableCollection<ManualColorGamutResultRow> resultRows = new();
        private bool isInitialized;
        private bool isUpdatingText;
        private bool isUpdatingSelection;

        public ManualColorGamutWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            standardOptions.AddRange(CieColorGamutStandards.All.Select(item => new StandardOption(item)));
            StandardOption? defaultOption = standardOptions.FirstOrDefault(item => string.Equals(item.Standard.Name, CieGamuts.SRgb.Name, StringComparison.Ordinal))
                ?? standardOptions.FirstOrDefault();
            if (defaultOption != null)
            {
                defaultOption.IsSelected = true;
            }

            ListBoxStandards.ItemsSource = standardOptions;
            ListBoxStandards.SelectedItem = defaultOption;
            ResultGrid.ItemsSource = resultRows;

            CieDiagram.SetDiagram(CieDiagramKind.Cie1931xy);
            CieDiagram.ShowCctReference = false;
            CieDiagram.ShowDaylightReference = false;
            CieDiagram.SetReferenceMarkers(Array.Empty<CieMarker>());

            ApplySelectedStandardToInputs();
            isInitialized = true;
            UpdateResult();
        }

        private void ListBoxStandards_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void StandardCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!isUpdatingSelection)
            {
                UpdateResult();
            }
        }

        private void CoordinateTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateResult();
        }

        private void ButtonSelectAll_Click(object sender, RoutedEventArgs e)
        {
            SetAllStandardsSelected(true);
        }

        private void ButtonClearSelection_Click(object sender, RoutedEventArgs e)
        {
            SetAllStandardsSelected(false);
        }

        private void ButtonApplyStandard_Click(object sender, RoutedEventArgs e)
        {
            ApplySelectedStandardToInputs();
            UpdateResult();
        }

        private void ButtonCalculate_Click(object sender, RoutedEventArgs e)
        {
            UpdateResult();
        }

        private void ButtonExport_Click(object sender, RoutedEventArgs e)
        {
            ExportResults();
        }

        private void SetAllStandardsSelected(bool isSelected)
        {
            isUpdatingSelection = true;
            try
            {
                foreach (StandardOption option in standardOptions)
                {
                    option.IsSelected = isSelected;
                }
            }
            finally
            {
                isUpdatingSelection = false;
            }

            UpdateResult();
        }

        private void ApplySelectedStandardToInputs()
        {
            StandardOption? option = ListBoxStandards.SelectedItem as StandardOption
                ?? standardOptions.FirstOrDefault(item => item.IsSelected)
                ?? standardOptions.FirstOrDefault();
            if (option == null)
            {
                return;
            }

            isUpdatingText = true;
            try
            {
                SetPrimaryText(TextBoxRedX, TextBoxRedY, option.Standard.Red);
                SetPrimaryText(TextBoxGreenX, TextBoxGreenY, option.Standard.Green);
                SetPrimaryText(TextBoxBlueX, TextBoxBlueY, option.Standard.Blue);
            }
            finally
            {
                isUpdatingText = false;
            }
        }

        private void UpdateResult()
        {
            if (!isInitialized || isUpdatingText)
            {
                return;
            }

            CieColorGamutStandard[] standards = GetSelectedStandards();
            if (standards.Length == 0)
            {
                ClearResult("请选择至少一个标准色域。", standards);
                return;
            }

            if (!TryReadInput(out CieGamutPrimary red, out CieGamutPrimary green, out CieGamutPrimary blue, out string error))
            {
                ClearResult(error, standards);
                return;
            }

            try
            {
                List<CieColorGamutCalculationResult> results = standards
                    .Select(standard => DefaultCieColorGamutCalculator.Calculate(red, green, blue, standard))
                    .ToList();

                resultRows.Clear();
                for (int index = 0; index < results.Count; index++)
                {
                    resultRows.Add(new ManualColorGamutResultRow(index + 1, results[index]));
                }

                double sampleArea = DefaultCieColorGamutCalculator.TriangleArea(red, green, blue);
                TextBlockSampleArea.Text = sampleArea.ToString("F6", CultureInfo.InvariantCulture);
                TextBlockResultCount.Text = resultRows.Count.ToString(CultureInfo.InvariantCulture);
                TextBlockCoverageRange.Text = resultRows.Count == 0
                    ? "-"
                    : $"{resultRows.Min(item => item.Result.CoveragePercent):F2}% - {resultRows.Max(item => item.Result.CoveragePercent):F2}%";
                TextBlockStatus.Foreground = Brushes.Gray;
                TextBlockStatus.Text = "计算完成。表格按标准色域逐行显示面积比，导出会包含当前全部结果。";
                ButtonExport.IsEnabled = resultRows.Count > 0;
                RenderDiagram(results, standards);
            }
            catch (Exception ex)
            {
                ClearResult(ex.Message, standards);
            }
        }

        private void ClearResult(string message, IReadOnlyList<CieColorGamutStandard> standards)
        {
            resultRows.Clear();
            TextBlockSampleArea.Text = "-";
            TextBlockResultCount.Text = "0";
            TextBlockCoverageRange.Text = "-";
            TextBlockStatus.Foreground = Brushes.IndianRed;
            TextBlockStatus.Text = message;
            ButtonExport.IsEnabled = false;
            RenderDiagram(Array.Empty<CieColorGamutCalculationResult>(), standards);
        }

        private CieColorGamutStandard[] GetSelectedStandards()
        {
            return standardOptions
                .Where(item => item.IsSelected)
                .Select(item => item.Standard)
                .ToArray();
        }

        private bool TryReadInput(
            out CieGamutPrimary red,
            out CieGamutPrimary green,
            out CieGamutPrimary blue,
            out string error)
        {
            red = default;
            green = default;
            blue = default;
            error = string.Empty;

            return TryReadPrimary("R", TextBoxRedX, TextBoxRedY, out red, out error)
                && TryReadPrimary("G", TextBoxGreenX, TextBoxGreenY, out green, out error)
                && TryReadPrimary("B", TextBoxBlueX, TextBoxBlueY, out blue, out error);
        }

        private static bool TryReadPrimary(string channel, TextBox xBox, TextBox yBox, out CieGamutPrimary primary, out string error)
        {
            primary = default;
            error = string.Empty;

            if (!TryParseDouble(xBox.Text, out double x) || !TryParseDouble(yBox.Text, out double y))
            {
                error = $"{channel} 坐标格式无效。";
                return false;
            }

            primary = new CieGamutPrimary(x, y);
            if (!primary.IsFinite)
            {
                error = $"{channel} 坐标不是有效数字。";
                return false;
            }

            const double tolerance = 0.000001;
            if (x < -tolerance || y < -tolerance || x > 1 + tolerance || y > 1 + tolerance || x + y > 1 + tolerance)
            {
                error = $"{channel} 坐标应满足 0 <= x <= 1、0 <= y <= 1 且 x + y <= 1。";
                return false;
            }

            return true;
        }

        private static bool TryParseDouble(string? text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return true;
            }

            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static void SetPrimaryText(TextBox xBox, TextBox yBox, CieGamutPrimary primary)
        {
            xBox.Text = primary.X.ToString("F4", CultureInfo.InvariantCulture);
            yBox.Text = primary.Y.ToString("F4", CultureInfo.InvariantCulture);
        }

        private void RenderDiagram(IReadOnlyList<CieColorGamutCalculationResult> results, IReadOnlyList<CieColorGamutStandard> standards)
        {
            List<CieGamut> gamuts = new();
            foreach (CieColorGamutStandard standard in standards)
            {
                gamuts.Add(CreateStandardGamut(standard));
            }

            List<CieMarker> markers = new();
            CieColorGamutCalculationResult? firstResult = results.Count > 0 ? results[0] : null;
            if (firstResult != null)
            {
                gamuts.Add(CreateManualGamut(firstResult));
                markers.Add(new CieMarker("R", firstResult.Red.ToChromaticity(), Color.FromRgb(222, 71, 64)));
                markers.Add(new CieMarker("G", firstResult.Green.ToChromaticity(), Color.FromRgb(56, 166, 82)));
                markers.Add(new CieMarker("B", firstResult.Blue.ToChromaticity(), Color.FromRgb(60, 123, 246)));
            }

            CieDiagram.SetGamuts(gamuts);
            CieDiagram.SetMarkers(markers);
            CieDiagram.ClearSelection();
            CieDiagram.ZoomUniform();
        }

        private void ExportResults()
        {
            if (resultRows.Count == 0)
            {
                MessageBox.Show(this, "没有可导出的计算结果。", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new()
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = "csv",
                FileName = $"ManualColorGamut_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                WriteCsv(dialog.FileName, BuildCsvRows());
                MessageBox.Show(this, $"导出成功：{dialog.FileName}", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"导出失败：{ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private IEnumerable<IReadOnlyList<string>> BuildCsvRows()
        {
            yield return new[]
            {
                "Standard",
                "Redx",
                "Redy",
                "Greenx",
                "Greeny",
                "Bluex",
                "Bluey",
                "StandardRedx",
                "StandardRedy",
                "StandardGreenx",
                "StandardGreeny",
                "StandardBluex",
                "StandardBluey",
                "SampleArea",
                "StandardArea",
                "AreaRatio(%)"
            };

            foreach (ManualColorGamutResultRow row in resultRows)
            {
                CieColorGamutCalculationResult result = row.Result;
                yield return new[]
                {
                    result.Standard.Name,
                    FormatDouble(result.Red.X),
                    FormatDouble(result.Red.Y),
                    FormatDouble(result.Green.X),
                    FormatDouble(result.Green.Y),
                    FormatDouble(result.Blue.X),
                    FormatDouble(result.Blue.Y),
                    FormatDouble(result.Standard.Red.X),
                    FormatDouble(result.Standard.Red.Y),
                    FormatDouble(result.Standard.Green.X),
                    FormatDouble(result.Standard.Green.Y),
                    FormatDouble(result.Standard.Blue.X),
                    FormatDouble(result.Standard.Blue.Y),
                    FormatDouble(result.SampleArea),
                    FormatDouble(result.StandardArea),
                    FormatDouble(result.CoveragePercent)
                };
            }
        }

        private static void WriteCsv(string filePath, IEnumerable<IReadOnlyList<string>> rows)
        {
            StringBuilder builder = new();
            foreach (IReadOnlyList<string> row in rows)
            {
                AppendCsvLine(builder, row);
            }

            File.WriteAllText(filePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        private static void AppendCsvLine(StringBuilder builder, IReadOnlyList<string> values)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append(EscapeCsvField(values[index]));
            }

            builder.AppendLine();
        }

        private static string EscapeCsvField(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
            {
                return value;
            }

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("F6", CultureInfo.InvariantCulture);
        }

        private static CieGamut CreateStandardGamut(CieColorGamutStandard standard)
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
                    standard.Red.ToChromaticity(),
                    standard.Green.ToChromaticity(),
                    standard.Blue.ToChromaticity()
                },
                Brushes.DimGray,
                new SolidColorBrush(Color.FromArgb(22, 64, 64, 64)));
        }

        private static CieGamut CreateManualGamut(CieColorGamutCalculationResult result)
        {
            Color accent = Color.FromRgb(255, 140, 66);
            SolidColorBrush stroke = new(accent);
            SolidColorBrush fill = new(Color.FromArgb(30, accent.R, accent.G, accent.B));
            return new CieGamut(
                "手动输入",
                new[]
                {
                    result.Red.ToChromaticity(),
                    result.Green.ToChromaticity(),
                    result.Blue.ToChromaticity()
                },
                stroke,
                fill);
        }

        private sealed class StandardOption : INotifyPropertyChanged
        {
            private bool isSelected;

            public StandardOption(CieColorGamutStandard standard)
            {
                Standard = standard;
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            public CieColorGamutStandard Standard { get; }

            public bool IsSelected
            {
                get => isSelected;
                set
                {
                    if (isSelected == value)
                    {
                        return;
                    }

                    isSelected = value;
                    OnPropertyChanged();
                }
            }

            private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private sealed class ManualColorGamutResultRow
        {
            public ManualColorGamutResultRow(int index, CieColorGamutCalculationResult result)
            {
                Index = index;
                Result = result;
            }

            public int Index { get; }
            public CieColorGamutCalculationResult Result { get; }
            public string StandardName => Result.Standard.Name;
            public string SampleAreaDisplay => Result.SampleArea.ToString("F6", CultureInfo.InvariantCulture);
            public string StandardAreaDisplay => Result.StandardArea.ToString("F6", CultureInfo.InvariantCulture);
            public string CoverageDisplay => Result.CoveragePercent.ToString("F2", CultureInfo.InvariantCulture);
            public string RedDisplay => FormatPrimary(Result.Red);
            public string GreenDisplay => FormatPrimary(Result.Green);
            public string BlueDisplay => FormatPrimary(Result.Blue);

            private static string FormatPrimary(CieGamutPrimary primary)
            {
                return $"({primary.X:F4}, {primary.Y:F4})";
            }
        }
    }
}
