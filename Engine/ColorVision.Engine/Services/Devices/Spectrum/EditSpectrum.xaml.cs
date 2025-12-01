using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.PhyCameras.Dao;
using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using ColorVision.Database;


namespace ColorVision.Engine.Services.Devices.Spectrum
{
    /// <summary>
    /// EditSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class EditSpectrum : Window
    {
        public DeviceSpectrum Device { get; set; }
        public ConfigSpectrum EditConfig {  get; set; }
        public EditSpectrum(DeviceSpectrum deviceSpectrum)
        {
            Device = deviceSpectrum;
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
            EditStackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(EditConfig));
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            EditConfig.CopyTo(Device.Config);
            Device.DService?.SetParam(Device.Config.MaxIntegralTime, Device.Config.BeginIntegralTime);
            Close();
        }
    }
}
