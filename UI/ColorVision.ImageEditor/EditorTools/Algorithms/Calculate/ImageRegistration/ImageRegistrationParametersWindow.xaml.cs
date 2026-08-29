using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.UI;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImageRegistration
{
    public partial class ImageRegistrationParametersWindow : Window
    {
        private readonly IAlgorithmCatalog _catalog;

        public ImageRegistrationParametersWindow()
            : this(ImageAlgorithmPlatform.Catalog)
        {
        }

        public ImageRegistrationParametersWindow(IAlgorithmCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            InitializeComponent();
            UpdateSummary();
        }

        public ImageRegistrationParameters Parameters { get; private set; } = new();

        public string? PresetId { get; private set; }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            bool submitted = false;
            PropertyEditorWindow editor = new(Parameters, PropertyEditorEditMode.Transactional)
            {
                Owner = this,
                Title = "图像配准参数",
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
                (string presetId, ImageRegistrationParameters parameters) = ImageRegistrationPresetSerializer.Deserialize(_catalog, File.ReadAllText(dialog.FileName, Encoding.UTF8));
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
                FileName = string.IsNullOrWhiteSpace(PresetId) ? "image-registration.json" : $"{PresetId}.json",
                AddExtension = true,
                OverwritePrompt = false,
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                if (File.Exists(dialog.FileName)) throw new IOException($"拒绝覆盖已有文件：{dialog.FileName}");
                string presetId = Path.GetFileNameWithoutExtension(dialog.FileName);
                string json = ImageRegistrationPresetSerializer.Serialize(_catalog, presetId, Parameters);
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
                "Preset: {0}\n方法: {1}    插值: {2}    边界: {3}\n相位最小响应: {4:G6}    最大平移: {5:G8}px\nORB 特征上限: {6:N0}    最少匹配/内点: {7}/{8}    重投影阈值: {9:G6}px",
                PresetId ?? "(未命名)", Parameters.Method, Parameters.Interpolation, Parameters.Border,
                Parameters.MinimumPhaseResponse, Parameters.MaximumTranslationPixels, Parameters.MaximumFeatures,
                Parameters.MinimumMatchCount, Parameters.MinimumInlierCount, Parameters.ConsensusReprojectionThresholdPixels);
        }
    }
}
