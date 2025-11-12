using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Sensor.Templates;
using ColorVision.Engine.Templates;
using ColorVision.Engine.ToolPlugins;
using ColorVision.UI;
using ColorVision.UI.Menus;
using CVCommCore;
using MQTTMessageLib;
using MQTTMessageLib.Sensor;
using System;
using System.IO.Ports;
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

            this.ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new SSCOMTool().ToMenuItem());
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Property, Command = Device.PropertyCommand });

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
                        SetVisibility(ButtonOpen, Visibility.Visible);
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
                        $"串口 {portName} 正在被其他程序占用，无法打开。\n\n请关闭相机或其他占用该串口的程序或检查设备连接。",
                        "串口被占用",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return;
                }
                catch (ArgumentException)
                {
                    MessageBox.Show(
                        Application.Current.GetActiveWindow(),
                        $"串口名称 {portName} 无效或不存在。\n\n请检查串口设置。",
                        "无效串口",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        Application.Current.GetActiveWindow(),
                        $"串口 {portName} 无法打开。\n\n异常信息：{ex.Message}",
                        "串口异常",
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
            msgRecord.MsgRecordStateChanged += (e) =>
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "打开成功");
            };
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

        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked = !ToggleButton0.IsChecked;
        }
    }
}
