#pragma warning disable CA1822,CA1863
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
using System.Windows.Data;

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
            ConfigTabs.SelectedItem = EditConfig.CameraMode == CameraMode.CV_MODE ? CfwTab : CameraTab;
            _isInitializing = false;
        }

        private void InitializeComboSources()
        {
            ComboxCameraModel.ItemsSource = Enum.GetValues<CameraModel>()
                .Select(item => new KeyValuePair<CameraModel, string>(item, item.ToDescription()));

            var cameraModes = Enum.GetValues<CameraMode>();
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
            AddGeneratedField(CfwConfigPanel, EditConfig.CFW, nameof(CFWPORT.IsUseCFW), Properties.Resources.LabelEnableCFW);
            AddGeneratedField(CfwConfigPanel, EditConfig.CFW, nameof(CFWPORT.IsBingNDDevice), Properties.Resources.LabelBindNDDevice, nameof(CFWPORT.IsUseCFW));
            AddGeneratedField(CfwConfigPanel, EditConfig.CFW, nameof(CFWPORT.NDBindDeviceCode), "ND 设备", nameof(CFWPORT.IsUseCFW));
            AddGeneratedField(CfwConfigPanel, EditConfig.CFW, nameof(CFWPORT.IsCOM), Properties.Resources.EnableSerialPort, nameof(CFWPORT.IsUseCFW));
            AddGeneratedField(CfwConfigPanel, EditConfig.CFW, nameof(CFWPORT.SzComName), Properties.Resources.Serial, nameof(CFWPORT.IsUseCFW));
            AddGeneratedField(CfwConfigPanel, EditConfig.CFW, nameof(CFWPORT.BaudRate), Properties.Resources.BaudRate, nameof(CFWPORT.IsUseCFW));
            AddGeneratedField(CfwConfigPanel, EditConfig.CFW, nameof(CFWPORT.CFWNum), Properties.Resources.LabelFilterWheelCount, nameof(CFWPORT.IsUseCFW));
            AddGeneratedField(CfwConfigPanel, EditConfig.CFW, nameof(CFWPORT.EnableResetND), "重置 ND", nameof(CFWPORT.IsUseCFW));
            AddGeneratedField(CfwConfigPanel, EditConfig.CFW, nameof(CFWPORT.IsNDPort), "ND 端口", nameof(CFWPORT.IsUseCFW));
            AddGeneratedField(CfwConfigPanel, EditConfig.CFW, nameof(CFWPORT.NDMaxExpTime), "最大曝光", nameof(CFWPORT.IsUseCFW));
            AddGeneratedField(CfwConfigPanel, EditConfig.CFW, nameof(CFWPORT.NDMinExpTime), "最小曝光", nameof(CFWPORT.IsUseCFW));
            AddGeneratedField(CfwConfigPanel, EditConfig.CFW, nameof(CFWPORT.NDRate), "倍率", nameof(CFWPORT.IsUseCFW));
            AddGeneratedField(CfwConfigPanel, EditConfig.CFW, nameof(CFWPORT.NDCaliNameGroups), "校准组", nameof(CFWPORT.IsUseCFW));

            AddGeneratedPanel(CameraCfgPanel, EditConfig.CameraCfg);
            AddGeneratedPanel(FileServerPanel, EditConfig.FileServerCfg);
            AddGeneratedPanel(MotorConfigPanel, EditConfig.MotorConfig);
            AddGeneratedPanel(CameraParameterLimitPanel, EditConfig.CameraParameterLimit);
        }

        private void RebuildGeneratedFields()
        {
            CfwConfigPanel.Children.Clear();
            CameraCfgPanel.Children.Clear();
            FileServerPanel.Children.Clear();
            MotorConfigPanel.Children.Clear();
            CameraParameterLimitPanel.Children.Clear();
            InitializeGeneratedFields();
        }

        private void AddGeneratedField(Panel panel, object config, string propertyName, string? labelText = null, string? visibleWhenProperty = null)
        {
            try
            {
                var field = PropertyEditorHelper.GenProperties(config, propertyName);
                field.HorizontalAlignment = HorizontalAlignment.Stretch;
                field.VerticalAlignment = VerticalAlignment.Top;
                NormalizeGeneratedText(field);

                if (!string.IsNullOrWhiteSpace(labelText))
                {
                    var label = field.Children.OfType<TextBlock>().FirstOrDefault();
                    if (label != null)
                    {
                        label.Text = labelText;
                    }
                }

                if (string.IsNullOrWhiteSpace(visibleWhenProperty))
                {
                    panel.Children.Add(field);
                    return;
                }

                var host = new Border
                {
                    Child = field,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Top
                };
                BindBoolVisibility(host, config, visibleWhenProperty);
                panel.Children.Add(host);
            }
            catch
            {
            }
        }

        private void AddGeneratedPanel(Panel panel, object config)
        {
            var editor = PropertyEditorHelper.GenPropertyEditorControl(config);
            NormalizeGeneratedEditor(editor);
            if (editor.Children.Count > 0)
            {
                panel.Children.Add(editor);
            }
        }

        private void BindBoolVisibility(FrameworkElement element, object source, string path)
        {
            var binding = new Binding(path)
            {
                Source = source,
                Mode = BindingMode.OneWay
            };

            if (TryFindResource("bool2VisibilityConverter") is IValueConverter converter)
            {
                binding.Converter = converter;
            }

            element.SetBinding(UIElement.VisibilityProperty, binding);
        }

        private static void NormalizeGeneratedEditor(StackPanel editor)
        {
            editor.HorizontalAlignment = HorizontalAlignment.Stretch;
            editor.VerticalAlignment = VerticalAlignment.Top;
            NormalizeGeneratedText(editor);

            foreach (var border in editor.Children.OfType<Border>())
            {
                border.HorizontalAlignment = HorizontalAlignment.Stretch;
                border.VerticalAlignment = VerticalAlignment.Top;
                if (border.Child is FrameworkElement child)
                {
                    child.HorizontalAlignment = HorizontalAlignment.Stretch;
                    child.VerticalAlignment = VerticalAlignment.Top;
                }
            }
        }

        private static void NormalizeGeneratedText(DependencyObject element)
        {
            if (element is FrameworkElement frameworkElement)
            {
                frameworkElement.VerticalAlignment = VerticalAlignment.Top;
            }

            if (element is Control control)
            {
                control.FontWeight = FontWeights.Normal;
            }

            if (element is TextBlock textBlock)
            {
                textBlock.FontWeight = FontWeights.Normal;
            }

            foreach (var child in LogicalTreeHelper.GetChildren(element))
            {
                if (child is DependencyObject dependencyObject)
                {
                    NormalizeGeneratedText(dependencyObject);
                }
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

            if (EditConfig.CameraMode == CameraMode.CV_MODE && applyModeDefaults)
            {
                ConfigTabs.SelectedItem = CfwTab;
            }

            if (EditConfig.CameraMode != CameraMode.CV_MODE && ConfigTabs.SelectedItem == CfwTab)
            {
                ConfigTabs.SelectedItem = CameraTab;
            }
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
            if (EditConfig.TryGetHkRoiAlignmentWarning(out string warning))
            {
                ConfigTabs.SelectedItem = CameraTab;
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
