using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.UI;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.GeometricTransform
{
    public partial class GeometricTransformParametersWindow : Window
    {
        private readonly IAlgorithmCatalog _catalog;

        public GeometricTransformParametersWindow()
            : this(ImageAlgorithmPlatform.Catalog)
        {
        }

        public GeometricTransformParametersWindow(IAlgorithmCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            InitializeComponent();
            UpdateSummary();
        }

        public GeometricTransformParameters Parameters { get; private set; } = new();

        public string? PresetId { get; private set; }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            bool submitted = false;
            PropertyEditorWindow editor = new(Parameters, PropertyEditorEditMode.Transactional)
            {
                Owner = this,
                Title = "几何变换参数",
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
                (string presetId, GeometricTransformParameters parameters) = GeometricTransformPresetSerializer.Deserialize(_catalog, File.ReadAllText(dialog.FileName, Encoding.UTF8));
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
                FileName = string.IsNullOrWhiteSpace(PresetId) ? "geometric-transform.json" : $"{PresetId}.json",
                AddExtension = true,
                OverwritePrompt = false,
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                if (File.Exists(dialog.FileName)) throw new IOException($"拒绝覆盖已有文件：{dialog.FileName}");
                string presetId = Path.GetFileNameWithoutExtension(dialog.FileName);
                string json = GeometricTransformPresetSerializer.Serialize(_catalog, presetId, Parameters);
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
            double[] matrix = Parameters.Matrix;
            SummaryText.Text = string.Format(CultureInfo.InvariantCulture,
                "Preset: {0}\n类型: {1}    画布: {2}    插值: {3}    边界: {4}\n[{5:G8}, {6:G8}, {7:G8}]\n[{8:G8}, {9:G8}, {10:G8}]\n[{11:G8}, {12:G8}, {13:G8}]\n显式尺寸: {14} × {15}    自动留白: {16}px\n最大输出: {17:N0} pixels",
                PresetId ?? "(未命名)", Parameters.Kind, Parameters.Canvas, Parameters.Interpolation, Parameters.Border,
                matrix[0], matrix[1], matrix[2], matrix[3], matrix[4], matrix[5], matrix[6], matrix[7], matrix[8],
                Parameters.OutputWidth, Parameters.OutputHeight, Parameters.FitPaddingPixels, Parameters.MaximumOutputPixels);
        }
    }
}
