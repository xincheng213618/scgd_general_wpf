using ColorVision.Common.Extension;
using ColorVision.Common.MVVM;
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


namespace ColorVision.Services.Devices.FileServer
{
    /// <summary>
    /// EditFileServer.xaml 的交互逻辑
    /// </summary>
    public partial class EditFileServer: Window
    {
        public DeviceFileServer Device { get; set; }

        public ConfigFileServer EditConfig { get; set; }
        public EditFileServer(DeviceFileServer deviceFileServer)
        {
            Device = deviceFileServer;
            InitializeComponent();
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
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            EditConfig.CopyTo(Device.Config);
            Close();
        }
    }
}
