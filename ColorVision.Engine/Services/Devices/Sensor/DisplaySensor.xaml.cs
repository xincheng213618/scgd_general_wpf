using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Devices.Camera.Video;
using ColorVision.Engine.Services.Devices.Sensor.Templates;
using ColorVision.Engine.Templates;
using ColorVision.UI;
using CVCommCore;
using MQTTMessageLib;
using MQTTMessageLib.Sensor;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Sensor
{
    /// <summary>
    /// DisplaySMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplaySensor : UserControl, IDisPlayControl
    {

        public DeviceSensor Device { get; set; }
        private MQTTSensor DeviceService { get => Device.DService;  }
        public string DisPlayName => Device.Config.Name;

        public DisplaySensor(DeviceSensor device)
        {
            Device = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            ComboxSensorTemplate.ItemsSource = SensorHeYuan.SensorHeYuans;
            ComboBoxType.ItemsSource = Enum.GetValues(typeof(SensorCmdType));

            this.ApplyChangedSelectedColor(DisPlayBorder);

            Device.DService.DeviceStatusChanged += (e) =>
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

            void UpdateUI(DeviceStatusType status)
            {
                void SetVisibility(UIElement element, Visibility visibility) => element.Visibility = visibility;
                void HideAllButtons()
                {
                    SetVisibility(ButtonOpen, Visibility.Collapsed);
                    SetVisibility(ButtonClose, Visibility.Collapsed);
                    SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                    SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                    SetVisibility(StackPanelContent, Visibility.Collapsed);
                }
                // Default state
                HideAllButtons();

                switch (status)
                {
                    case DeviceStatusType.Unauthorized:
                        SetVisibility(ButtonUnauthorized, Visibility.Visible);
                        break;
                    case DeviceStatusType.Unknown:
                        SetVisibility(TextBlockUnknow, Visibility.Visible);
                        break;
                    case DeviceStatusType.OffLine:
                        break;
                    case DeviceStatusType.UnInit:
                        break;
                    case DeviceStatusType.Closed:
                        SetVisibility(ButtonOpen, Visibility.Visible);
                        break;
                    case DeviceStatusType.LiveOpened:
                    case DeviceStatusType.Opened:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        SetVisibility(ButtonClose, Visibility.Visible);
                        break;
                    case DeviceStatusType.Closing:
                    case DeviceStatusType.Opening:
                    default:
                        // No specific action needed
                        break;
                }
            }
            UpdateUI(Device.DService.DeviceStatus);
            Device.DService.DeviceStatusChanged += UpdateUI;

        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }


        private void Open_Click(object sender, RoutedEventArgs e)
        {
             DeviceService.Open();
        }


        private void SendCommand_Click(object sender, RoutedEventArgs e)
        {
            if (ComboBoxType.SelectedItem is SensorCmdType CmdType)
            {
                SensorCmd cmd = new() { CmdType = CmdType, Request = TextBoxSendCommand.Text, Response = TextBoxResCommand.Text, Timeout = 5000, Delay = 0, RetryCount = 1 };
                DeviceService.ExecCmd(cmd);
            }
        }

        private void SendTemp_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxSensorTemplate.SelectedItem is TemplateModel<SensorHeYuan> sensorHeYuan)
            {
                CVTemplateParam templateParam = new() { ID= sensorHeYuan.Value.Id, Name= sensorHeYuan.Value.Name };
                DeviceService.ExecCmd(templateParam);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DeviceService.Close();
        }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            new WindowTemplate(new TemplateSensorHeYuan()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }
}
