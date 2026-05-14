using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Themes;
using ColorVision.UI;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Services.PhyCameras
{
    /// <summary>
    /// EditCfwPortConfig.xaml 的交互逻辑
    /// </summary>
    public partial class EditCfwPortConfig : Window
    {
        private readonly CFWPORT _sourceConfig;

        public CFWPORT EditConfig { get; }

        public EditCfwPortConfig(CFWPORT config)
        {
            _sourceConfig = config ?? throw new ArgumentNullException(nameof(config));
            EditConfig = config.CloneForEdit();
            EditConfig.EnsureChannelCfgsForEdit();

            InitializeComponent();
            this.ApplyCaption();
            DataContext = EditConfig;

            InitializeChannelOptions();
            InitializeGeneratedFields();
        }

        private void InitializeChannelOptions()
        {
            var imageChannelTypeList = new[]
            {
                new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_X, "Channel_R"),
                new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_Y, "Channel_G"),
                new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_Z, "Channel_B")
            };

            chType1.ItemsSource = imageChannelTypeList;
            chType2.ItemsSource = imageChannelTypeList;
            chType3.ItemsSource = imageChannelTypeList;
        }

        private void InitializeGeneratedFields()
        {
            AddGeneratedField(ConnectionFieldsPanel, nameof(CFWPORT.NDBindDeviceCode));
            AddGeneratedField(ConnectionFieldsPanel, nameof(CFWPORT.SzComName));
            AddGeneratedField(ConnectionFieldsPanel, nameof(CFWPORT.BaudRate));

            AddGeneratedField(AdvancedFieldsPanel, nameof(CFWPORT.EnableResetND));
            AddGeneratedField(AdvancedFieldsPanel, nameof(CFWPORT.IsNDPort));
            AddGeneratedField(AdvancedFieldsPanel, nameof(CFWPORT.NDMaxExpTime));
            AddGeneratedField(AdvancedFieldsPanel, nameof(CFWPORT.NDMinExpTime));
            AddGeneratedField(AdvancedFieldsPanel, nameof(CFWPORT.NDRate));
            AddGeneratedField(AdvancedFieldsPanel, nameof(CFWPORT.NDCaliNameGroups));
        }

        private void AddGeneratedField(System.Windows.Controls.Panel panel, string propertyName)
        {
            try
            {
                panel.Children.Add(PropertyEditorHelper.GenProperties(EditConfig, propertyName));
            }
            catch
            {
            }
        }

        private void ResetChannels_Click(object sender, RoutedEventArgs e)
        {
            EditConfig.ChannelCfgs.Clear();
            EditConfig.EnsureChannelCfgsForEdit();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            EditConfig.NormalizeChannelCfgsForSave();
            EditConfig.CopyTo(_sourceConfig);
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
