using MQTTMessageLib;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Device.Motor
{
    /// <summary>
    /// DisplaySMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayMotorControl : UserControl
    {

        public DeviceMotor Device { get; set; }
        private MQTTMotor DeviceService { get => Device.DeviceService;  }

        public DisplayMotorControl(DeviceMotor device)
        {
            this.Device = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Device;
            DeviceService.DeviceStatusChanged += DeviceService_DeviceStatusChanged;
        }

        private void DeviceService_DeviceStatusChanged(DeviceStatusType deviceStatus)
        {
            switch (deviceStatus)
            {
                case DeviceStatusType.Closed:
                    ButtonSwitch.Content = "连接";
                    break;
                case DeviceStatusType.Opened:
                    ButtonSwitch.Content = "关闭";
                    break;
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (DeviceService.DeviceStatus == DeviceStatusType.Closed && button.Content.ToString() == "连接")
                {
                    var msgRecord = DeviceService.Open();
                    Helpers.SendCommand(button, msgRecord);
                }
                else if (DeviceService.DeviceStatus == DeviceStatusType.Opened && button.Content.ToString() == "关闭")
                {
                    var msgRecord = DeviceService.Close();
                    Helpers.SendCommand(button, msgRecord);
                }
                else
                {
                    MessageBox.Show(Application.Current.MainWindow,"指令已经发送请稍等","ColorVision");
                }
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
                if (double.TryParse(TextDiaphragm.Text, out double pos))
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

        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked = !ToggleButton0.IsChecked;
        }
    }
}
