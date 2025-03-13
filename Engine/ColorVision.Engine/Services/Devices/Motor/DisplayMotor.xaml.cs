using ColorVision.Themes.Controls;
using ColorVision.UI;
using CVCommCore;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Motor
{
    /// <summary>
    /// DisplaySMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayMotor : UserControl, IDisPlayControl
    {

        public DeviceMotor Device { get; set; }
        private MQTTMotor DeviceService { get => Device.DService;  }
        public string DisPlayName => Device.Config.Name;

        public DisplayMotor(DeviceMotor device)
        {
            Device = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            DeviceService.DeviceStatusChanged += DeviceService_DeviceStatusChanged;
            this.ApplyChangedSelectedColor(DisPlayBorder);
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }

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
                if ( button.Content.ToString() == "连接")
                {
                    var msgRecord = DeviceService.Open();
                    ServicesHelper.SendCommand(button, msgRecord);
                }
                else if (button.Content.ToString() == "关闭")
                {
                    var msgRecord = DeviceService.Close();
                    ServicesHelper.SendCommand(button, msgRecord);
                }
                else
                {
                    MessageBox1.Show(Application.Current.MainWindow,"指令已经发送请稍等","ColorVision");
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
                    ServicesHelper.SendCommand(button, msgRecord);
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
                    ServicesHelper.SendCommand(button, msgRecord);
                }
            }
        }

        private void GetPosition_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DeviceService.GetPosition();
                ServicesHelper.SendCommand(button, msgRecord);
            }
        }

        private void Move1_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (double.TryParse(TextDiaphragm.Text, out double pos))
                {
                    var msgRecord = DeviceService.MoveDiaphragm(pos);
                    ServicesHelper.SendCommand(button, msgRecord);
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DeviceService.Close();
                ServicesHelper.SendCommand(button, msgRecord);
            }
        }

        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked = !ToggleButton0.IsChecked;
        }
    }
}
