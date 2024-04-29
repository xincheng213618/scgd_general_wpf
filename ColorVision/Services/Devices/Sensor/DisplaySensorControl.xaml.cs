using ColorVision.Common.Utilities;
using ColorVision.Services.Devices.Sensor.Templates;
using ColorVision.Services.Templates;
using ColorVision.Themes;
using ColorVision.UI;
using MQTTMessageLib;
using MQTTMessageLib.Sensor;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Services.Devices.Sensor
{
    /// <summary>
    /// DisplaySMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplaySensorControl : UserControl, IDisPlayControl
    {

        public DeviceSensor Device { get; set; }
        private MQTTSensor DeviceService { get => Device.DeviceService;  }

        public DisplaySensorControl(DeviceSensor device)
        {
            Device = device;
            InitializeComponent();

            PreviewMouseDown += UserControl_PreviewMouseDown;
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            ComboxSensorTemplate.ItemsSource = SensorHeYuan.SensorHeYuans;
            ComboBoxType.ItemsSource = Enum.GetValues(typeof(SensorCmdType));

            SelectChanged += (s, e) =>
            {
                DisPlayBorder.BorderBrush = IsSelected ? ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#5649B0" : "#A79CF1") : ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#EAEAEA" : "#151515");
            };
            ThemeManager.Current.CurrentUIThemeChanged += (s) =>
            {
                DisPlayBorder.BorderBrush = IsSelected ? ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#5649B0" : "#A79CF1") : ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#EAEAEA" : "#151515");
            };

            if (Device.Config.IsNet)
            {
                TextBlockPGIP.Text = "IP地址";
                TextBlockPGPort.Text = "端口";
            }
            else
            {
                TextBlockPGIP.Text = "串口";
                TextBlockPGPort.Text = "波特率";
            }

            Device.DeviceService.DeviceStatusChanged += (e) =>
            {
                switch (e)
                {
                    case DeviceStatusType.Opened:
                        ButtonClose.Visibility = Visibility.Visible;
                        ButtonOpen.Visibility = Visibility.Collapsed;
                        break;
                    case DeviceStatusType.Closed:
                        ButtonOpen.Visibility = Visibility.Visible;
                        ButtonClose.Visibility = Visibility.Collapsed;
                        break;
                    default:
                        ButtonOpen.Visibility = Visibility.Visible;
                        ButtonClose.Visibility = Visibility.Collapsed;
                        break;
                }
            };
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }


        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Parent is StackPanel stackPanel)
            {
                if (stackPanel.Tag is IDisPlayControl disPlayControl)
                    disPlayControl.IsSelected = false;
                stackPanel.Tag = this;
                IsSelected = true;
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
             DeviceService.Open();
        }


        private void SendCommand_Click(object sender, RoutedEventArgs e)
        {
            if (ComboBoxType.SelectedItem is SensorCmdType CmdType)
            {
                SensorCmd cmd = new SensorCmd() { CmdType = CmdType, Request = TextBoxSendCommand.Text, Response = TextBoxResCommand.Text, Timeout = 5000 };
                DeviceService.ExecCmd(cmd);
            }
        }

        private void SendTemp_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxSensorTemplate.SelectedItem is TemplateModel<SensorHeYuan> sensorHeYuan)
            {
                CVTemplateParam templateParam = new CVTemplateParam() { ID= sensorHeYuan.Value.Id, Name= sensorHeYuan.Value.Name };
                DeviceService.ExecCmd(templateParam);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DeviceService.Close();
        }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            new WindowTemplate(TemplateType.SensorHeYuan,false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }
}
