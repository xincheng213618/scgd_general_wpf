using ColorVision.Common.MVVM;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Algorithm;
using ColorVision.Services.Devices.Calibration;
using ColorVision.Services.Devices.Camera;
using ColorVision.Services.Devices;
using ColorVision.Services.Devices.Camera.Configs;
using ColorVision.Services.Devices.CfwPort;
using ColorVision.Services.Devices.FileServer;
using ColorVision.Services.Devices.Motor;
using ColorVision.Services.Devices.PG;
using ColorVision.Services.Devices.Sensor;
using ColorVision.Services.Devices.SMU.Configs;
using ColorVision.Services.Devices.SMU;
using ColorVision.Services.Devices.Spectrum.Configs;
using ColorVision.Services.Devices.Spectrum;
using ColorVision.Services.Type;
using ColorVision.Settings;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ColorVision.Common.Utilities;
using ColorVision.Services.RC;
using ColorVision.Services.Terminal;

namespace ColorVision.Services.Templates
{
    /// <summary>
    /// EditTerminal.xaml 的交互逻辑
    /// </summary>
    public partial class CreateTemplate : Window
    {

        public CreateTemplate()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }
    }
}
