using ColorVision.Common.Utilities;
using ColorVision.Services.Core;
using ColorVision.Themes;
using MQTTMessageLib;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Services.Devices.Motor
{
    /// <summary>
    /// DisplaySMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayMotorControl : UserControl, IDisPlayControl
    {

        public DeviceMotor Device { get; set; }
        private MQTTMotor DeviceService { get => Device.DeviceService;  }

        public DisplayMotorControl(DeviceMotor device)
        {
            this.Device = device;
            InitializeComponent();

            this.PreviewMouseDown += UserControl_PreviewMouseDown;
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Device;
            DeviceService.DeviceStatusChanged += DeviceService_DeviceStatusChanged;
        }
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; DisPlayBorder.BorderBrush = value ? ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#5649B0" : "#A79CF1") : ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#EAEAEA" : "#151515");  } }
        private bool _IsSelected;

        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (this.Parent is StackPanel stackPanel)
            {
                if (stackPanel.Tag is IDisPlayControl disPlayControl)
                    disPlayControl.IsSelected = false;
                stackPanel.Tag = this;
                IsSelected = true;
            }
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
                    ServicesHelper.SendCommand(button, msgRecord);
                }
                else if (DeviceService.DeviceStatus == DeviceStatusType.Opened && button.Content.ToString() == "关闭")
                {
                    var msgRecord = DeviceService.Close();
                    ServicesHelper.SendCommand(button, msgRecord);
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
