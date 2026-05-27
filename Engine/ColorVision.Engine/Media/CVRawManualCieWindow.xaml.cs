using ColorVision.Themes;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Media
{
    public partial class CVRawManualCieWindow : Window
    {
        public event EventHandler? Submited;

        public CVRawManualCieConfig Config { get; }

        public CVRawManualCieConfig EditConfig { get; } = new();

        private IReadOnlyList<LumFourColorCalibrationFileItem> CalibrationFiles { get; }

        public CVRawManualCieWindow(CVRawManualCieConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            CopyConfig(Config, EditConfig);
            CalibrationFiles = PhyCameraManager.GetInstance().GetLumFourColorCalibrationFiles();

            InitializeComponent();
            this.ApplyCaption();
            CalibrationFileList.ItemsSource = CalibrationFiles;
            CalibrationFileList.SelectedIndex = CalibrationFiles.Count > 0 ? 0 : -1;
            RenderEditor();
        }

        private void RenderEditor()
        {
            PropertyPanelHost.Children.Clear();
            PropertyPanelHost.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(EditConfig));
        }

        private void ImportSelected_Click(object sender, RoutedEventArgs e)
        {
            if (CalibrationFileList.SelectedItem is not LumFourColorCalibrationFileItem selectedItem)
            {
                MessageBox.Show(this, ColorVision.Engine.Properties.Resources.Engine_Msg_SelectCalibrationFileFirst, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!CVRawManualCieCalculator.TryLoadLumFourColorCalibrationDefaults(selectedItem.FilePath, out CVRawManualCieConfig importedConfig, out string? errorMessage))
            {
                MessageBox.Show(this, errorMessage ?? ColorVision.Engine.Properties.Resources.Engine_Msg_ImportFourColorCalFailed, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            CopyConfig(importedConfig, EditConfig);
            RenderEditor();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            CopyConfig(Config, EditConfig);
            RenderEditor();
        }

        private void ResetToFactory_Click(object sender, RoutedEventArgs e)
        {
            CopyConfig(CVRawManualCieConfig.CreateFactoryDefaults(), EditConfig);
            RenderEditor();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            CopyConfig(EditConfig, Config);
            Submited?.Invoke(this, EventArgs.Empty);
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private static void CopyConfig(CVRawManualCieConfig source, CVRawManualCieConfig target)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);

            target.Gain_x = source.Gain_x;
            target.Gain_y = source.Gain_y;
            target.Gain_z = source.Gain_z;
            target.Texp_x = source.Texp_x;
            target.Texp_y = source.Texp_y;
            target.Texp_z = source.Texp_z;
            target.A = source.A;
            target.B = source.B;
            target.C = source.C;
            target.D = source.D;
            target.E = source.E;
            target.F = source.F;
            target.G = source.G;
            target.H = source.H;
            target.I = source.I;
        }
    }
}