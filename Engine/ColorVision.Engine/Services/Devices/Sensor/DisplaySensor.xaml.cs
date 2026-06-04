using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Sensor.Templates;
using ColorVision.Engine.Templates;
using ColorVision.UI;
using MQTTMessageLib;
using MQTTMessageLib.Sensor;
using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Sensor
{
    /// <summary>
    /// DisplaySensor.xaml 的交互逻辑
    /// </summary>
    public partial class DisplaySensor : UserControl, IDisPlayControl,IDisposable
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
            ComboBoxType.ItemsSource = Enum.GetValues<SensorCmdType>();

            this.ApplyChangedSelectedColor(DisPlayBorder);
            DService_DeviceStatusChanged(sender,Device.DService.DeviceStatus);
            Device.DService.DeviceStatusChanged += DService_DeviceStatusChanged;

        }

        private void DService_DeviceStatusChanged(object? sender, DeviceStatusType e)
        {
            void SetVisibility(UIElement element, Visibility visibility) { if (element.Visibility != visibility) element.Visibility = visibility; }
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

            switch (e)
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
                    SetVisibility(ButtonOpen, Visibility.Visible);
                    break;
            }
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }


        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (!DeviceService.Config.IsNet)
            {
                string portName = DeviceService.Config.Addr;
                int baudRate = DeviceService.Config.Port;
                // 1. 检查串口是否可用
                SerialPort serialPort = null;
                try
                {
                    serialPort = new SerialPort { PortName = portName, BaudRate = baudRate };
                    serialPort.Open();
                    serialPort.Close();
                }
                catch (UnauthorizedAccessException)
                {
                    MessageBox.Show(
                        Application.Current.GetActiveWindow(),
                        string.Format(Properties.Resources.SerialPortOccupied, portName),
                        Properties.Resources.SerialPortOccupiedTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return;
                }
                catch (ArgumentException)
                {
                    MessageBox.Show(
                        Application.Current.GetActiveWindow(),
                        string.Format(Properties.Resources.InvalidSerialPort, portName),
                        Properties.Resources.InvalidSerialPortTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        Application.Current.GetActiveWindow(),
                        string.Format(Properties.Resources.SerialPortError, portName, ex.Message),
                        Properties.Resources.SerialPortErrorTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return;
                }
                finally
                {
                    serialPort?.Dispose();
                }
            }

            MsgRecord msgRecord = DeviceService.Open();
            msgRecord.MsgRecordStateChanged += (s,e) =>
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.OpenSuccess);
            };
        }


        private void SendCommand_Click(object sender, RoutedEventArgs e)
        {
            if (ComboBoxType.SelectedItem is SensorCmdType CmdType)
            {
                bool ische = IsAddNewLine.IsChecked ?? false;
                string sendcmd = TextBoxSendCommand.Text;

                // 处理转义字符：将字面字符串转换为实际的转义字符
                sendcmd = sendcmd.Replace("\\r\\n", "\r\n")
                                 .Replace("\\n", "\n")
                                 .Replace("\\r", "\r")
                                 .Replace("\\t", "\t");

                sendcmd += ische ? "\n" : "";

                SensorCmd cmd = new()
                {
                    CmdType = CmdType,
                    Request = sendcmd,
                    Response = TextBoxResCommand.Text,
                    Timeout = 5000,
                    Delay = 0,
                    RetryCount = 1
                };
                MsgRecord msgRecord = DeviceService.ExecCmd(cmd);
                msgRecord.MsgRecordStateChanged += (s,e) =>
                {
                    MessageBox.Show(e.ToString());
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

        public void Dispose()
        {
            Device.DService.DeviceStatusChanged -= DService_DeviceStatusChanged;
        }
    }
}
