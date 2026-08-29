using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.UI;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.LensDistortionCorrection
{
    public partial class LensDistortionCorrectionParametersWindow : Window
    {
        private readonly IAlgorithmCatalog _catalog;

        public LensDistortionCorrectionParametersWindow()
            : this(ImageAlgorithmPlatform.Catalog)
        {
        }

        public LensDistortionCorrectionParametersWindow(IAlgorithmCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            InitializeComponent();
            UpdateSummary();
        }

        public LensDistortionCorrectionParameters Parameters { get; private set; } = new();

        public string? PresetId { get; private set; }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            bool submitted = false;
            PropertyEditorWindow editor = new(Parameters, PropertyEditorEditMode.Transactional)
            {
                Owner = this,
                Title = "镜头畸变校正参数",
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            editor.Submitted += (_, _) => submitted = true;
            editor.ShowDialog();
            if (submitted)
            {
                PresetId = null;
                UpdateSummary();
            }
        }

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new() { Filter = "ColorVision 算法 preset (*.json)|*.json|所有文件 (*.*)|*.*", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                (string presetId, LensDistortionCorrectionParameters parameters) = LensDistortionCorrectionPresetSerializer.Deserialize(_catalog, File.ReadAllText(dialog.FileName, Encoding.UTF8));
                PresetId = presetId;
                Parameters = parameters;
                UpdateSummary();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                Filter = "ColorVision 算法 preset (*.json)|*.json",
                FileName = string.IsNullOrWhiteSpace(PresetId) ? "lens-distortion-correction.json" : $"{PresetId}.json",
                AddExtension = true,
                OverwritePrompt = false,
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                if (File.Exists(dialog.FileName)) throw new IOException($"拒绝覆盖已有文件：{dialog.FileName}");
                string presetId = Path.GetFileNameWithoutExtension(dialog.FileName);
                string json = LensDistortionCorrectionPresetSerializer.Serialize(_catalog, presetId, Parameters);
                using FileStream stream = File.Open(dialog.FileName, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using StreamWriter writer = new(stream, new UTF8Encoding(false));
                writer.Write(json);
                PresetId = presetId;
                UpdateSummary();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            var validation = Parameters.Validate();
            if (!validation.IsValid)
            {
                MessageBox.Show(this, string.Join("; ", validation.Issues), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void UpdateSummary()
        {
            SummaryText.Text = string.Format(CultureInfo.InvariantCulture,
                "Preset: {0}\n内参: Fx={1:G10}px  Fy={2:G10}px  主点={3} ({4:G10}, {5:G10})px\n畸变: K1={6:G8}  K2={7:G8}  P1={8:G8}  P2={9:G8}  K3={10:G8}  K4={11:G8}  K5={12:G8}  K6={13:G8}\n输出: {14}  Alpha={15:G5}  插值={16}  边界={17}\n标定: source={18}  version={19}  checksum={20}",
                PresetId ?? "(未命名)", Parameters.FxPixels, Parameters.FyPixels, Parameters.PrincipalPointMode,
                Parameters.PrincipalPointX, Parameters.PrincipalPointY,
                Parameters.K1, Parameters.K2, Parameters.P1, Parameters.P2, Parameters.K3, Parameters.K4, Parameters.K5, Parameters.K6,
                Parameters.OutputCameraMode, Parameters.OptimalAlpha, Parameters.Interpolation, Parameters.Border,
                Parameters.CalibrationSource, Parameters.CalibrationVersion,
                string.IsNullOrWhiteSpace(Parameters.CalibrationChecksum) ? "(未提供)" : Parameters.CalibrationChecksum);
        }
    }
}
