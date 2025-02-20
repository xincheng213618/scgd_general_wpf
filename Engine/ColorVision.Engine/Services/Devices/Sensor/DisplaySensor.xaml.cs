using ColorVision.Engine.Messages;
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
            void Update()
            {
                var list = new TemplateSensor(Device.Config.Category);
                list.Load();
                ComboxSensorTemplate.ItemsSource = list.TemplateParams;
            }
            Device.ConfigChanged += (s, e) => Update();
            Update();
            ComboBoxType.ItemsSource = Enum.GetValues(typeof(SensorCmdType));

            this.ApplyChangedSelectedColor(DisPlayBorder);


            void UpdateUI(DeviceStatusType status)
            {
                void SetVisibility(UIElement element, Visibility visibility) { if (element.Visibility != visibility) element.Visibility = visibility; };
                void HideAllButtons()
                {
                    SetVisibility(ButtonOpen, Visibility.Collapsed);
                    SetVisibility(ButtonClose, Visibility.Collapsed);
                    SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                    SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                    SetVisibility(StackPanelContent, Visibility.Collapsed);
                    SetVisibility(TextBlockOffLine, Visibility.Collapsed);

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
                        SetVisibility(TextBlockOffLine, Visibility.Visible);
                        break;
                    case DeviceStatusType.UnInit:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        SetVisibility(ButtonOpen, Visibility.Visible);
                        break;
                    case DeviceStatusType.Closed:
                        SetVisibility(StackPanelContent, Visibility.Visible);
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
                        SetVisibility(StackPanelContent, Visibility.Visible);
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
                MsgRecord msgRecord = DeviceService.ExecCmd(cmd);
                msgRecord.MsgRecordStateChanged += (s) =>
                {
                    MessageBox.Show(s.ToString());
                };
            }
        }

        private void SendTemp_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxSensorTemplate.SelectedItem is TemplateModel<SensorParam> sensorHeYuan)
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
            new TemplateEditorWindow(new TemplateSensor(Device.Config.Category)) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }
}
