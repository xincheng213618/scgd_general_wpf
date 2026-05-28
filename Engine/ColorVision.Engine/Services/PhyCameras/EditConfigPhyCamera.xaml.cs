using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.PhyCameras
{
    /// <summary>
    /// EditConfigPhyCamera.xaml 的交互逻辑
    /// </summary>
    public partial class EditConfigPhyCamera : Window
    {
        private bool _isInitializing = true;

        public PhyCamera PhyCamera { get; }
        public ConfigPhyCamera EditConfig { get; }

        public EditConfigPhyCamera(PhyCamera phyCamera)
        {
            PhyCamera = phyCamera ?? throw new ArgumentNullException(nameof(phyCamera));
            EditConfig = PhyCamera.Config.Clone();
            EditConfig.CFW = PhyCamera.Config.CFW.CloneForEdit();
            EditConfig.CFW.EnsureChannelCfgsForEdit();

            InitializeComponent();
            this.ApplyCaption();
            DataContext = this;

            InitializeComboSources();
            InitializeChannelOptions();
            InitializeGeneratedFields();
            UpdateChannelOptions(false);
            _isInitializing = false;
        }

        private void InitializeComboSources()
        {
            ComboxCameraModel.ItemsSource = Enum.GetValues<CameraModel>()
                .Select(item => new KeyValuePair<CameraModel, string>(item, item.ToDescription()));

            var cameraModes = new[] { CameraMode.BV_MODE, CameraMode.LV_MODE, CameraMode.CV_MODE };
            if (!cameraModes.Contains(EditConfig.CameraMode))
            {
                EditConfig.CameraMode = CameraMode.BV_MODE;
            }

            ComboxCameraMode.ItemsSource = cameraModes
                .Select(item => new KeyValuePair<CameraMode, string>(item, item.ToDescription()));

            ComboxCameraImageBpp.ItemsSource = Enum.GetValues<ImageBpp>()
                .Select(item => new KeyValuePair<ImageBpp, string>(item, item.ToDescription()));
        }

        private void InitializeChannelOptions()
        {
            var imageChannelTypeList = new[]
            {
                new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_X, Properties.Resources.ChannelR),
                new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_Y, Properties.Resources.ChannelG),
                new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_Z, Properties.Resources.ChannelB)
            };

            chType1.ItemsSource = imageChannelTypeList;
            chType2.ItemsSource = imageChannelTypeList;
            chType3.ItemsSource = imageChannelTypeList;
        }

        private void InitializeGeneratedFields()
        {
            AddGeneratedField(CfwConnectionFieldsPanel, EditConfig.CFW, nameof(CFWPORT.NDBindDeviceCode));
            AddGeneratedField(CfwConnectionFieldsPanel, EditConfig.CFW, nameof(CFWPORT.SzComName));
            AddGeneratedField(CfwConnectionFieldsPanel, EditConfig.CFW, nameof(CFWPORT.BaudRate));

            AddGeneratedPanel(FileServerPanel, EditConfig.FileServerCfg);
            AddGeneratedPanel(CameraCfgPanel, EditConfig.CameraCfg);
            AddGeneratedPanel(MotorConfigPanel, EditConfig.MotorConfig);
            AddGeneratedPanel(CameraParameterLimitPanel, EditConfig.CameraParameterLimit);

            AddGeneratedField(CfwAdvancedFieldsPanel, EditConfig.CFW, nameof(CFWPORT.EnableResetND));
            AddGeneratedField(CfwAdvancedFieldsPanel, EditConfig.CFW, nameof(CFWPORT.IsNDPort));
            AddGeneratedField(CfwAdvancedFieldsPanel, EditConfig.CFW, nameof(CFWPORT.NDMaxExpTime));
            AddGeneratedField(CfwAdvancedFieldsPanel, EditConfig.CFW, nameof(CFWPORT.NDMinExpTime));
            AddGeneratedField(CfwAdvancedFieldsPanel, EditConfig.CFW, nameof(CFWPORT.NDRate));
            AddGeneratedField(CfwAdvancedFieldsPanel, EditConfig.CFW, nameof(CFWPORT.NDCaliNameGroups));
        }

        private void RebuildGeneratedFields()
        {
            CfwConnectionFieldsPanel.Children.Clear();
            FileServerPanel.Children.Clear();
            CameraCfgPanel.Children.Clear();
            MotorConfigPanel.Children.Clear();
            CameraParameterLimitPanel.Children.Clear();
            CfwAdvancedFieldsPanel.Children.Clear();
            InitializeGeneratedFields();
        }

        private void AddGeneratedField(Panel panel, object config, string propertyName)
        {
            try
            {
                panel.Children.Add(PropertyEditorHelper.GenProperties(config, propertyName));
            }
            catch
            {
            }
        }

        private void AddGeneratedPanel(Panel panel, object config)
        {
            var editor = PropertyEditorHelper.GenPropertyEditorControl(config);
            if (editor.Children.Count > 0)
            {
                panel.Children.Add(editor);
            }
        }

        private void ComboxCameraMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            UpdateChannelOptions(true);
        }

        private void UpdateChannelOptions(bool applyModeDefaults)
        {
            List<ImageChannel> channels;
            if (EditConfig.CameraMode == CameraMode.BV_MODE)
            {
                channels = new List<ImageChannel> { ImageChannel.One, ImageChannel.Three };
                if (EditConfig.Channel != ImageChannel.One && EditConfig.Channel != ImageChannel.Three)
                {
                    EditConfig.Channel = ImageChannel.Three;
                }

                if (applyModeDefaults)
                {
                    EditConfig.CFW.IsUseCFW = false;
                }
            }
            else if (EditConfig.CameraMode == CameraMode.CV_MODE)
            {
                channels = new List<ImageChannel> { ImageChannel.Three };
                EditConfig.Channel = ImageChannel.Three;
                if (applyModeDefaults)
                {
                    EditConfig.CFW.IsUseCFW = true;
                }
            }
            else
            {
                channels = new List<ImageChannel> { ImageChannel.One };
                EditConfig.Channel = ImageChannel.One;
                if (applyModeDefaults)
                {
                    EditConfig.CFW.IsUseCFW = false;
                }
            }

            ComboxCameraChannel.ItemsSource = channels
                .Select(item => new KeyValuePair<ImageChannel, string>(item, item.ToDescription()));
            ComboxCameraChannel.IsEnabled = EditConfig.CameraMode == CameraMode.BV_MODE;
            ChannelField.Visibility = EditConfig.CameraMode == CameraMode.LV_MODE ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ResetChannels_Click(object sender, RoutedEventArgs e)
        {
            EditConfig.CFW.ChannelCfgs.Clear();
            EditConfig.CFW.EnsureChannelCfgsForEdit();
        }

        private void OpenPropertyEditor_Click(object sender, RoutedEventArgs e)
        {
            var propertyEditorWindow = new PropertyEditorWindow(EditConfig, isEdit: false)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            propertyEditorWindow.Submited += (s, e) =>
            {
                _isInitializing = true;
                EditConfig.CFW.EnsureChannelCfgsForEdit();
                InitializeComboSources();
                UpdateChannelOptions(false);
                DataContext = null;
                DataContext = this;
                RebuildGeneratedFields();
                _isInitializing = false;
            };
            propertyEditorWindow.ShowDialog();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
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
