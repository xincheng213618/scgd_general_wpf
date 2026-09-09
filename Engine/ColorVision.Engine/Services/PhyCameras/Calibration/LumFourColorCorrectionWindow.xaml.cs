using ColorVision.Common.ThirdPartyApps;
using ColorVision.Common.MVVM;
using ColorVision.Engine.Media;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.PhyCameras.Calibration
{
    public sealed class CorrectionMeasurementRow : ViewModelBase
    {
        public string Target { get; init; } = string.Empty;
        private string cameraY = "", cameraX = "", cameraYChromaticity = "", referenceY = "", referenceX = "", referenceYChromaticity = "";
        public string CameraY { get => cameraY; set { if (cameraY == value) return; cameraY = value; OnPropertyChanged(); } }
        public string CameraX { get => cameraX; set { if (cameraX == value) return; cameraX = value; OnPropertyChanged(); } }
        public string CameraYChromaticity { get => cameraYChromaticity; set { if (cameraYChromaticity == value) return; cameraYChromaticity = value; OnPropertyChanged(); } }
        public string ReferenceY { get => referenceY; set { if (referenceY == value) return; referenceY = value; OnPropertyChanged(); } }
        public string ReferenceX { get => referenceX; set { if (referenceX == value) return; referenceX = value; OnPropertyChanged(); } }
        public string ReferenceYChromaticity { get => referenceYChromaticity; set { if (referenceYChromaticity == value) return; referenceYChromaticity = value; OnPropertyChanged(); } }
    }

    public partial class LumFourColorCorrectionWindow : Window
    {
        private readonly ObservableCollection<CorrectionMeasurementRow> rows = new();
        private CVRawManualCieConfig? correctedConfig;
        private LumFourColorSourceSnapshot? sourceSnapshot;

        public LumFourColorCorrectionWindow(string? sourcePath = null)
        {
            InitializeComponent();
            this.ApplyCaption();
            rows.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                    foreach (CorrectionMeasurementRow row in e.NewItems)
                        row.PropertyChanged += (_, _) => ResetResult();
            };
            MeasurementsGrid.ItemsSource = rows;
            CommandManager.AddPreviewExecutedHandler(MeasurementsGrid, GridPreviewExecuted);
            SourcePathBox.Text = sourcePath ?? string.Empty;
            ShowFourColorRows();
        }

        public static void ShowWindow(string? sourcePath = null)
        {
            LumFourColorCorrectionWindow? existing = Application.Current.Windows
                .OfType<LumFourColorCorrectionWindow>()
                .FirstOrDefault();
            if (existing != null)
            {
                if (!string.IsNullOrWhiteSpace(sourcePath))
                {
                    existing.SourcePathBox.Text = sourcePath;
                    existing.ResetResult();
                }
                if (existing.WindowState == WindowState.Minimized)
                {
                    existing.WindowState = WindowState.Normal;
                }
                existing.Activate();
                return;
            }

            Window? owner = Application.Current.GetActiveWindow();
            LumFourColorCorrectionWindow window = new(sourcePath)
            {
                Owner = owner,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            };
            window.Show();
        }

        private void BrowseSource_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Title = "选择原四色校正文件",
                Filter = "四色校正文件 (*.dat;*.json)|*.dat;*.json|所有文件 (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
            };
            if (dialog.ShowDialog(this) == true)
            {
                SourcePathBox.Text = dialog.FileName;
                ResetResult();
            }
        }

        private void CorrectionMode_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized || MeasurementsGrid == null)
                return;

            if (SinglePointMode.IsChecked == true)
            {
                rows.Clear();
                rows.Add(new CorrectionMeasurementRow { Target = "单点" });
            }
            else
            {
                ShowFourColorRows();
            }
            ResetResult();
        }

        private void ShowFourColorRows()
        {
            rows.Clear();
            rows.Add(new CorrectionMeasurementRow { Target = "R" });
            rows.Add(new CorrectionMeasurementRow { Target = "G" });
            rows.Add(new CorrectionMeasurementRow { Target = "B" });
            rows.Add(new CorrectionMeasurementRow { Target = "W" });
        }

        private void GridPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command != ApplicationCommands.Paste) return;
            e.Handled = true;
            Paste_Click(sender, e);
        }

        private void MeasurementsGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            if (e.Key == Key.V)
            {
                e.Handled = true;
                Paste_Click(sender, e);
            }
            else if (e.Key == Key.C)
            {
                MeasurementsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                MeasurementsGrid.CommitEdit(DataGridEditingUnit.Row, true);
                e.Handled = true;
                ApplicationCommands.Copy.Execute(null, MeasurementsGrid);
            }
        }

        internal void PasteMeasurements(string text)
        {
            int row = MeasurementsGrid.CurrentCell.Item is CorrectionMeasurementRow current ? rows.IndexOf(current) : 0;
            int column = MeasurementsGrid.CurrentCell.Column?.DisplayIndex ?? 1;
            MeasurementsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            MeasurementsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            LumFourColorMeasurementClipboard.Paste(rows, text, row, column);
            ResetResult();
            StatusText.Text = "已粘贴，请核对色块顺序与测量来源。";
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            try { PasteMeasurements(Clipboard.GetText()); }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MeasurementsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                MeasurementsGrid.CommitEdit(DataGridEditingUnit.Row, true);
                Clipboard.SetText(LumFourColorMeasurementClipboard.CopyAll(rows));
                StatusText.Text = "已复制表头及全部测量数据，可直接粘贴到 Excel。";
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                correctedConfig = null;
                SaveButton.IsEnabled = false;
                MeasurementsGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
                MeasurementsGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
                if (ManualDataConfirmed.IsChecked != true)
                    throw new InvalidOperationException("请先核对输入来源、色块及光谱 IP。");
                sourceSnapshot = LumFourColorSourceSnapshot.Load(SourcePathBox.Text.Trim());
                CVRawManualCieConfig source = sourceSnapshot.Config;
                if (SinglePointMode.IsChecked == true)
                {
                    correctedConfig = LumFourColorCorrectionCalculator.CorrectSinglePoint(source, CreateMeasurement(rows[0]));
                }
                else
                {
                    if (rows.Count != 4)
                        throw new InvalidOperationException("四色修正需要 R、G、B、W 四组测量值。");

                    correctedConfig = LumFourColorCorrectionCalculator.CorrectFourColor(source, new LumFourColorCorrectionMeasurements(
                        CreateMeasurement(rows[0]),
                        CreateMeasurement(rows[1]),
                        CreateMeasurement(rows[2]),
                        CreateMeasurement(rows[3])));
                }

                ResultPreview.Text = FormatMatrix(correctedConfig);
                StatusText.Text = "计算完成";
                SaveButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (correctedConfig == null)
                return;

            string sourcePath = SourcePathBox.Text.Trim();
            string sourceDirectory = Path.GetDirectoryName(sourcePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
            string extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(extension))
                extension = ".dat";

            SaveFileDialog dialog = new()
            {
                Title = "保存修正后的四色校正文件",
                Filter = "四色校正文件 (*.dat)|*.dat|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                InitialDirectory = Directory.Exists(sourceDirectory) ? sourceDirectory : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                FileName = $"{sourceName}_Corrected{extension}",
                AddExtension = true,
                DefaultExt = extension.TrimStart('.'),
            };
            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                sourceSnapshot!.SaveCopy(dialog.FileName, correctedConfig);
                StatusText.Text = $"已保存：{dialog.FileName}";
            }
            catch (Exception ex)
            {
                ShowError($"保存失败：{ex.Message}");
            }
        }

        private static ColorCorrectionMeasurement CreateMeasurement(CorrectionMeasurementRow row)
        {
            ColorCorrectionYxy camera = new(
                ParseNumber(row.CameraY, $"{row.Target} 相机 Y"),
                ParseNumber(row.CameraX, $"{row.Target} 相机 CIE x"),
                ParseNumber(row.CameraYChromaticity, $"{row.Target} 相机 CIE y"));
            ColorCorrectionYxy reference = new(
                ParseNumber(row.ReferenceY, $"{row.Target} 光谱 Y"),
                ParseNumber(row.ReferenceX, $"{row.Target} 光谱 CIE x"),
                ParseNumber(row.ReferenceYChromaticity, $"{row.Target} 光谱 CIE y"));
            return new ColorCorrectionMeasurement(camera, reference);
        }

        private static double ParseNumber(string text, string name)
        {
            const NumberStyles styles = NumberStyles.Float;
            if ((!double.TryParse(text, styles, CultureInfo.CurrentCulture, out double value)
                    && !double.TryParse(text, styles, CultureInfo.InvariantCulture, out value))
                || !double.IsFinite(value))
            {
                throw new InvalidOperationException($"{name} 必须是有限数值。");
            }
            return value;
        }

        private static string FormatMatrix(CVRawManualCieConfig config)
        {
            return string.Join(Environment.NewLine,
                FormatRow(config.A, config.B, config.C),
                FormatRow(config.D, config.E, config.F),
                FormatRow(config.G, config.H, config.I));
        }

        private static string FormatRow(double first, double second, double third)
        {
            return $"{first,16:G10}  {second,16:G10}  {third,16:G10}";
        }

        private void ResetResult()
        {
            correctedConfig = null;
            sourceSnapshot = null;
            if (ManualDataConfirmed != null) ManualDataConfirmed.IsChecked = false;
            if (ResultPreview != null)
                ResultPreview.Text = string.Empty;
            if (StatusText != null)
                StatusText.Text = string.Empty;
            if (SaveButton != null)
                SaveButton.IsEnabled = false;
        }

        private void ShowError(string message)
        {
            ResetResult();
            StatusText.Text = message;
            SaveButton.IsEnabled = false;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void InputChanged(object sender, RoutedEventArgs e) => ResetResult();
    }

    public sealed class LumFourColorCorrectionAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            yield return new ThirdPartyAppInfo
            {
                Name = "四色校正采集",
                Group = "ColorVision",
                Category = ThirdPartyAppCategory.Internal,
                RequiredPermission = PermissionMode.Administrator,
                Order = 12,
                IconGlyph = ThirdPartyAppIconGlyphs.DataFile,
                LaunchAction = () => LumFourColorCalibrationWorkflowWindow.ShowWindow(),
            };
        }
    }
}
