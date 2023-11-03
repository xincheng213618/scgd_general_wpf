using ColorVision.Device;
using ColorVision.Services.Msg;
using ColorVision.Templates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Device.Motor
{
    /// <summary>
    /// SMUDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayMotorControl : UserControl
    {

        public DeviceMotor Device { get; set; }
        private DeviceServiceMotor DeviceService { get => Device.DeviceService;  }

        public DisplayMotorControl(DeviceMotor device)
        {
            this.Device = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Device;

        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DeviceService.Open();
                Helpers.SendCommand(button, msgRecord);
            }
        }

        private void Move_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (int.TryParse(TextPos.Text ,out int pos))
                {
                    var msgRecord = DeviceService.Move(pos,CheckBoxIsAbs.IsChecked??true);
                    Helpers.SendCommand(button, msgRecord);
                }
            }
        }

        private void GoHome_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (int.TryParse(TextPos.Text, out int pos))
                {
                    var msgRecord = DeviceService.GoHome();
                    Helpers.SendCommand(button, msgRecord);
                }
            }
        }

        private void GetPosition_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DeviceService.GetPosition();
                Helpers.SendCommand(button, msgRecord);
            }
        }

        private void Move1_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (int.TryParse(TextDiaphragm.Text, out int pos))
                {
                    var msgRecord = DeviceService.MoveDiaphragm(pos);
                    Helpers.SendCommand(button, msgRecord);
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DeviceService.Close();
                Helpers.SendCommand(button, msgRecord);
            }
        }
    }
}
