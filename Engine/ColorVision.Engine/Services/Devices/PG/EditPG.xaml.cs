using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Input;


namespace ColorVision.Engine.Services.Devices.PG
{
    /// <summary>
    /// EditPG.xaml 的交互逻辑
    /// </summary>
    public partial class EditPG : Window
    {
        private const string GecsV24Category = "GECS.V2.4";

        public DevicePG Device { get; set; }

        public ConfigPG EditConfig { get; set; }

        public EditPG(DevicePG devicePG)
        {
            Device = devicePG;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;

            EditConfig = Device.Config.Clone();
            EditContent.DataContext = EditConfig;

            Device.DService.ReLoadCategoryLib();

            CH341_Stream_SpeedComboBox.ItemsSource = Enum.GetValues<CH341_Stream_Speed>();

            pgCategory.SelectionChanged += (s, e) =>
            {
                if (pgCategory.SelectedItem is not KeyValuePair<string, Dictionary<string, string>> selectedCategory)
                    return;

                EditConfig.Category = selectedCategory.Key;
                if (selectedCategory.Key == "CH431.I2C")
                {
                    EditConfig.Addr = "0";
                    EditConfig.Port = 0x08;
                    RegisterAddressDockPanel.Visibility = Visibility.Visible;

                    TextBlockPGIP.Text = Properties.Resources.SzComName;
                    TextBlockPGPort.Text = Properties.Resources.DeviceAddr;

                    CH341_Stream_SpeedDock.Visibility = Visibility.Visible;

                }
                else
                {
                    CH341_Stream_SpeedDock.Visibility = Visibility.Collapsed;
                    RegisterAddressDockPanel.Visibility = Visibility.Collapsed;
                    TextBlockPGIP.Text = Properties.Resources.IPAddress;
                    TextBlockPGPort.Text = Properties.Resources.Port;
                }

                RefreshGeneratedPropertyEditor();
            };

            pgCategory.ItemsSource = Device.DService.PGCategoryLib;

            foreach (var item in Device.DService.PGCategoryLib)
            {
                if (item.Key.Equals(Device.Config.Category, StringComparison.Ordinal))
                {
                    pgCategory.SelectedItem = item;
                    break;
                }
            }

            if (EditConfig.Category == "CH431.I2C")
            {
                TextBlockPGIP.Text = Properties.Resources.SzComName;
                TextBlockPGPort.Text = Properties.Resources.DeviceAddr;
            }


            RefreshGeneratedPropertyEditor();
        }

        private void RefreshGeneratedPropertyEditor()
        {
            GeneratedPropertyEditorHost.Children.Clear();
            GeneratedPropertyEditorHost.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(
                EditConfig,
                metadataProvider: new ConfigPGMetadataProvider(EditConfig)));
        }

        private sealed class ConfigPGMetadataProvider(ConfigPG config) : IPropertyEditorMetadataProvider
        {
            public bool IsPropertyManaged(PropertyInfo propertyInfo) => true;

            public bool IsBrowsable(PropertyInfo propertyInfo) => propertyInfo.GetCustomAttribute<BrowsableAttribute>()?.Browsable ?? true;

            public Type? GetEditorType(PropertyInfo propertyInfo) => null;

            public string? GetDisplayName(PropertyInfo propertyInfo)
            {
                return propertyInfo.Name == nameof(ConfigPG.RegisterAddress)
                    && string.Equals(config.Category, GecsV24Category, StringComparison.Ordinal)
                    ? nameof(Properties.Resources.Channel)
                    : null;
            }

            public string? GetDescription(PropertyInfo propertyInfo) => null;

            public string? GetCategory(PropertyInfo propertyInfo) => null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            EditConfig.CopyTo(Device.Config);
            Close();
        }
    }
}
