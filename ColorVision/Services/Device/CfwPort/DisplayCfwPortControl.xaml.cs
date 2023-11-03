using ColorVision.Device;
using ColorVision.Templates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Device.CfwPort
{
    /// <summary>
    /// SMUDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayCfwPortControl : UserControl
    {

        public DeviceCfwPort Device { get; set; }
        private DeviceServiceCfwPort DeviceService { get => Device.DeviceService;  }

        public DisplayCfwPortControl(DeviceCfwPort device)
        {
            this.Device = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Device;




        }

        private void GetPort_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DeviceService.GetPort();
                Helpers.SendCommand(button, msgRecord);
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DeviceService.Open();
                Helpers.SendCommand(button, msgRecord);
            }

        }

        private void SetPort_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (int.TryParse(TextPort.Text,out int port))
                {
                    var msgRecord = DeviceService.SetPort(port);
                    Helpers.SendCommand(button, msgRecord);
                }
            }
        }
    }
}
