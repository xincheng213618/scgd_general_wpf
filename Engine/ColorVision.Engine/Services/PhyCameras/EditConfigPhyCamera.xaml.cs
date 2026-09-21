#pragma warning disable CA1822,CA1863
using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using System;
using System.IO;
using System.Windows;

namespace ColorVision.Engine.Services.PhyCameras
{
    /// <summary>
    /// EditConfigPhyCamera.xaml 的交互逻辑
    /// </summary>
    public partial class EditConfigPhyCamera : Window
    {
        public PhyCamera PhyCamera { get; }
        public ConfigPhyCamera EditConfig { get; }

        public EditConfigPhyCamera(PhyCamera phyCamera)
        {
            PhyCamera = phyCamera ?? throw new ArgumentNullException(nameof(phyCamera));
            EditConfig = PhyCamera.Config.Clone();
            EditConfig.CFW = PhyCamera.Config.CFW.CloneForEdit();

            InitializeComponent();
            this.ApplyCaption();
            DataContext = this;

            ConfigEditor.Initialize(EditConfig);
        }

        private void OpenPropertyEditor_Click(object sender, RoutedEventArgs e) => ConfigEditor.OpenPropertyEditor(this);

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (EditConfig.TryGetHkRoiAlignmentWarning(out string warning))
            {
                ConfigEditor.SelectCameraParameters();
                MessageBox1.Show(this, warning, Properties.Resources.TitleEditCameraConfig, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            EditConfig.CFW.NormalizeChannelCfgsForSave();
            HandleFileServerPathChanged();
            EditConfig.CopyTo(PhyCamera.Config);
            DialogResult = true;
            Close();
        }

        private void HandleFileServerPathChanged()
        {
            string oldBasePath = PhyCamera.Config.FileServerCfg.FileBasePath ?? string.Empty;
            string newBasePath = EditConfig.FileServerCfg.FileBasePath ?? string.Empty;
            if (string.Equals(oldBasePath, newBasePath, StringComparison.Ordinal))
            {
                return;
            }

            MessageBox1.Show(Properties.Resources.FileCopyServiceRestartHint);

            string sourceDir = Path.Combine(oldBasePath, PhyCamera.Code);
            string targetDir = Path.Combine(newBasePath, PhyCamera.Code);
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            if (!Directory.Exists(sourceDir))
            {
                return;
            }

            if (MessageBox1.Show(string.Format(Properties.Resources.AutoCopyFolderConfirm, sourceDir, newBasePath), Properties.Resources.TitleEditCameraConfig, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                if (!Directory.Exists(newBasePath))
                {
                    Directory.CreateDirectory(newBasePath);
                }

                try
                {
                    Common.NativeMethods.ShellFileOperations.Move(sourceDir, newBasePath);
                    MessageBox.Show(Properties.Resources.FolderCopySucceeded);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(Properties.Resources.FolderCopyFailed, ex.Message));
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
