using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.UI;
using Microsoft.Win32;
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImagingCorrection
{
    public partial class ImagingCorrectionParametersWindow : Window
    {
        private readonly IAlgorithmCatalog _catalog;

        public ImagingCorrectionParametersWindow()
            : this(ImageAlgorithmPlatform.Catalog)
        {
        }

        public ImagingCorrectionParametersWindow(IAlgorithmCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            InitializeComponent();
            DataContext = Parameters;
            UpdateSummary();
        }

        public ImagingCorrectionParameters Parameters { get; private set; } = new();

        public string? PresetId { get; private set; }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Filter = "图像文件|*.bmp;*.gif;*.ico;*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.webp|所有文件|*.*",
                CheckFileExists = true,
            };
            if (dialog.ShowDialog(this) != true) return;
            switch ((sender as Button)?.Tag as string)
            {
                case "dark": Parameters.DarkFramePath = dialog.FileName; Parameters.EnableDarkFrame = true; break;
                case "flat": Parameters.FlatFieldPath = dialog.FileName; Parameters.EnableFlatField = true; break;
                case "shading": Parameters.ShadingReferencePath = dialog.FileName; Parameters.EnableShading = true; break;
                case "bad": Parameters.BadPixelMapPath = dialog.FileName; Parameters.EnableBadPixelCorrection = true; break;
            }
            RefreshBindings();
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            bool submitted = false;
            PropertyEditorWindow editor = new(Parameters, PropertyEditorEditMode.Transactional)
            {
                Owner = this,
                Title = "成像校正高级参数",
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            editor.Submitted += (_, _) => submitted = true;
            editor.ShowDialog();
            if (submitted) { PresetId = null; RefreshBindings(); }
        }

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new() { Filter = "ColorVision 算法 preset (*.json)|*.json|所有文件 (*.*)|*.*", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                (string presetId, ImagingCorrectionParameters parameters) = ImagingCorrectionPresetSerializer.Deserialize(_catalog, File.ReadAllText(dialog.FileName, Encoding.UTF8));
                PresetId = presetId;
                Parameters = parameters;
                RefreshBindings();
            }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                Filter = "ColorVision 算法 preset (*.json)|*.json",
                FileName = string.IsNullOrWhiteSpace(PresetId) ? "imaging-correction.json" : $"{PresetId}.json",
                AddExtension = true,
                OverwritePrompt = false,
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                if (File.Exists(dialog.FileName)) throw new IOException($"拒绝覆盖已有文件：{dialog.FileName}");
                string presetId = Path.GetFileNameWithoutExtension(dialog.FileName);
                string json = ImagingCorrectionPresetSerializer.Serialize(_catalog, presetId, Parameters);
                using FileStream stream = File.Open(dialog.FileName, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using StreamWriter writer = new(stream, new UTF8Encoding(false));
                writer.Write(json);
                PresetId = presetId;
                UpdateSummary();
            }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
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

        private void RefreshBindings()
        {
            DataContext = null;
            DataContext = Parameters;
            UpdateSummary();
        }

        private void UpdateSummary()
            => SummaryText.Text = $"Preset: {PresetId ?? "(未命名)"}\n无效参考: {Parameters.InvalidReferencePolicy}；增益 {Parameters.MinimumGain:G5}..{Parameters.MaximumGain:G5}；输出 {Parameters.OutputRangePolicy}\n校正来源: {Parameters.CalibrationSource}；版本: {Parameters.CalibrationVersion}";
    }
}
