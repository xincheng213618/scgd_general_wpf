using ColorVision.Common.ThirdPartyApps;
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
using System.Text;
using System.Windows;

namespace ColorVision.Engine.Services.PhyCameras.Calibration
{
    public sealed class CorrectionMeasurementRow
    {
        public string Target { get; init; } = string.Empty;
        public string CameraY { get; set; } = string.Empty;
        public string CameraX { get; set; } = string.Empty;
        public string CameraYChromaticity { get; set; } = string.Empty;
        public string ReferenceY { get; set; } = string.Empty;
        public string ReferenceX { get; set; } = string.Empty;
        public string ReferenceYChromaticity { get; set; } = string.Empty;
    }

    public partial class LumFourColorCorrectionWindow : Window
    {
        private readonly ObservableCollection<CorrectionMeasurementRow> rows = new();
        private CVRawManualCieConfig? correctedConfig;

        public LumFourColorCorrectionWindow(string? sourcePath = null)
        {
            InitializeComponent();
            this.ApplyCaption();
            MeasurementsGrid.ItemsSource = rows;
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

        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            if (!CVRawManualCieCalculator.TryLoadLumFourColorCalibrationDefaults(
                    SourcePathBox.Text.Trim(), out CVRawManualCieConfig source, out string? errorMessage))
            {
                ShowError(errorMessage ?? "无法读取原四色校正文件。");
                return;
            }

            try
            {
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
                File.WriteAllText(dialog.FileName, LumFourColorCorrectionCalculator.SerializeCalibrationFile(correctedConfig), new UTF8Encoding(false));
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
            const NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;
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
            if (ResultPreview != null)
                ResultPreview.Text = string.Empty;
            if (StatusText != null)
                StatusText.Text = string.Empty;
            if (SaveButton != null)
                SaveButton.IsEnabled = false;
        }

        private void ShowError(string message)
        {
            correctedConfig = null;
            ResultPreview.Text = string.Empty;
            StatusText.Text = message;
            SaveButton.IsEnabled = false;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
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
