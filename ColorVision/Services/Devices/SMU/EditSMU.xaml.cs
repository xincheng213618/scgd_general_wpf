using ColorVision.Common.Extension;
using ColorVision.Common.MVVM;
using ColorVision.Services.Devices.Calibration;
using ColorVision.Services.Devices.SMU.Configs;
using cvColorVision;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Text;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace ColorVision.Services.Devices.SMU
{
    /// <summary>
    /// EditSMU.xaml 的交互逻辑
    /// </summary>
    public partial class EditSMU : Window
    {
        public DeviceSMU Device { get; set; }
        public ConfigSMU EditConfig {  get; set; }
        public EditSMU(DeviceSMU  deviceSMU)
        {
            Device = deviceSMU;
            InitializeComponent();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            List<string> Serials = new List<string> { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8" };
            TextSerial.ItemsSource = Serials;
            List<string> devTypes = new List<string> { "Keithley_2400", "Keithley_2600", "Precise_S100" };
            SMUType.ItemsSource = devTypes;
            DataContext = Device;

            EditConfig = Device.Config.Clone();
            EditContent.DataContext = EditConfig;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            EditConfig.CopyTo(Device.Config);
            Close();
        }
    }
}
