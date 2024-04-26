using ColorVision.Common.Extension;
using ColorVision.Services.Devices.PG;
using ColorVision.Services.Devices.Calibration;
using ColorVision.Services.Devices.Spectrum;
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
using ColorVision.Common.MVVM;


namespace ColorVision.Services.Devices.Sensor
{
    /// <summary>
    /// EditSensor.xaml 的交互逻辑
    /// </summary>
    public partial class EditSensor : Window
    {
        public DeviceSensor Device { get; set; }

        public ConfigSensor EditConfig { get; set; }

        public EditSensor(DeviceSensor device)
        {
            Device = device;
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
            IsComm.Checked += (s,e)=>
            {
                TextBlockPGIP.Text = "串口";
                TextBlockPGPort.Text = "波特率";
            };
            IsNet.Checked += (s,e)=> 
            {
                TextBlockPGIP.Text = "IP地址";
                TextBlockPGPort.Text = "端口";
            };

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
