using ColorVision.Device.FileServer;
using ColorVision.Device.PG;
using ColorVision.Services.Devices.Spectrum.Configs;
using ColorVision.MySql.DAO;
using ColorVision.Services.Devices;
using ColorVision.Services.Devices.Algorithm;
using ColorVision.Services.Devices.Calibration;
using ColorVision.Services.Devices.Camera;
using ColorVision.Services.Devices.Camera.Configs;
using ColorVision.Services.Devices.CfwPort;
using ColorVision.Services.Devices.Motor;
using ColorVision.Services.Devices.Sensor;
using ColorVision.Services.Devices.SMU;
using ColorVision.Services.Devices.SMU.Configs;
using ColorVision.Services.Devices.Spectrum;
using cvColorVision;
using Newtonsoft.Json;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColorVision.Services
{
    /// <summary>
    /// TerminalServiceControl.xaml 的交互逻辑
    /// </summary>
    public partial class TerminalServiceControl : UserControl
    {
        public TerminalService ServiceTerminal { get; set; }
        public ServiceManager ServiceControl { get; set; }

        public TerminalServiceControl(TerminalService mQTTService)
        {
            this.ServiceTerminal = mQTTService;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ServiceControl = ServiceManager.GetInstance();
            this.DataContext = ServiceTerminal;

            TextBox_Type.ItemsSource = ServiceTerminal.Parent.VisualChildren;
            TextBox_Type.SelectedItem = ServiceTerminal;

            if (ServiceTerminal.VisualChildren.Count == 0)
                ListViewService.Visibility = Visibility.Collapsed;
            ListViewService.ItemsSource = ServiceTerminal.VisualChildren;

            ServiceTerminal.VisualChildren.CollectionChanged += (s, e) =>
            {
                if (ServiceTerminal.VisualChildren.Count == 0)
                {
                    ListViewService.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ListViewService.Visibility = Visibility.Visible;
                }
            };
        }

        private SysResourceModel? saveConfigInfo(DeviceServiceConfig deviceConfig, SysResourceModel sysResource)
        {
            deviceConfig.Name = TextBox_Name.Text;
            deviceConfig.Code = TextBox_Code.Text;

            deviceConfig.SendTopic = ServiceTerminal.Config.SendTopic;
            deviceConfig.SubscribeTopic = ServiceTerminal.Config.SubscribeTopic;
            sysResource.Value = JsonConvert.SerializeObject(deviceConfig);
            ServiceControl.ResourceService.Save(sysResource);
            int pkId = sysResource.GetPK();
            if (pkId > 0 && ServiceControl.ResourceService.GetMasterById(pkId) is SysResourceModel model) return model;
            else return null;
        }
        private void Button_New_Click(object sender, RoutedEventArgs e)
        {
            if (TextBox_Type.SelectedItem is TerminalService serviceTerminal)
            {
                ///这里对相机做一个特殊处理
                if (serviceTerminal.Type != ServiceTypes.camera)
                {
                    if (!ServicesHelper.IsInvalidPath(TextBox_Name.Text, "资源名称") || !ServicesHelper.IsInvalidPath(TextBox_Code.Text, "资源标识"))
                        return;
                }
                if (serviceTerminal.ServicesCodes.Contains(TextBox_Code.Text))
                {
                    MessageBox.Show("设备标识已存在,不允许重复添加");
                    return;
                }
                SysResourceModel sysResource = new SysResourceModel(TextBox_Name.Text, TextBox_Code.Text, serviceTerminal.SysResourceModel.Type, serviceTerminal.SysResourceModel.Id, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);


                SysResourceModel sysResourceModel;
                DeviceServiceConfig deviceConfig;
                switch (serviceTerminal.Type)
                {
                    case ServiceTypes.camera:
                        ConfigCamera cameraConfig1 = new ConfigCamera
                        {
                            Id = TextBox_Code.Text,
                            Name = TextBox_Name.Text,
                            CameraType = CameraType.LV_Q,
                            TakeImageMode = TakeImageMode.Measure_Normal,
                            ImageBpp = ImageBpp.bpp8,
                            Channel = ImageChannel.One,
                            FileServerCfg = new FileServerCfg()
                            {
                                Endpoint = "tcp://127.0.0.1:" + (Math.Abs(new Random().Next()) % 99 + 6800),
                                DataBasePath = "D:\\CVTest",
                            }
                        };

                        sysResourceModel = saveConfigInfo(cameraConfig1, sysResource);
                        if (sysResourceModel != null)
                        {
                            if (serviceTerminal.MQTTServiceTerminalBase is MQTTTerminalCamera cameraService)
                            {
                                serviceTerminal.AddChild(new DeviceCamera(sysResourceModel, cameraService));
                            }
                        }
                        break;
                    case ServiceTypes.pg:
                        ConfigPG pGConfig = new ConfigPG
                        {
                            Id = TextBox_Code.Text,
                            Name = TextBox_Name.Text
                        };
                        sysResourceModel = saveConfigInfo(pGConfig, sysResource);
                        if (sysResourceModel != null)
                            serviceTerminal.AddChild(new DevicePG(sysResourceModel));
                        break;
                    case ServiceTypes.Spectum:
                        deviceConfig = new ConfigSpectrum
                        {
                            Id = TextBox_Code.Text,
                            Name = TextBox_Name.Text,
                            ShutterCfg = new ShutterConfig()
                            {
                                Addr = "COM1",
                                BaudRate = 115200,
                                DelayTime = 1000,
                                OpenCmd = "a",
                                CloseCmd = "b"
                            },
                        };
                        sysResourceModel = saveConfigInfo(deviceConfig, sysResource);
                        if (sysResourceModel != null)
                            serviceTerminal.AddChild(new DeviceSpectrum(sysResourceModel));
                        break;
                    case ServiceTypes.SMU:
                        deviceConfig = new ConfigSMU
                        {
                            Id = TextBox_Code.Text,
                            Name = TextBox_Name.Text
                        };
                        SysResourceModel model = saveConfigInfo(deviceConfig, sysResource);
                        if (model != null)
                            serviceTerminal.AddChild(new DeviceSMU(model));
                        break;
                    case ServiceTypes.Sensor:
                        deviceConfig = new ConfigSensor
                        {
                            Id = TextBox_Code.Text,
                            Name = TextBox_Name.Text
                        };
                        sysResourceModel = saveConfigInfo(deviceConfig, sysResource);
                        if (sysResourceModel != null)
                            serviceTerminal.AddChild(new DeviceSensor(sysResourceModel));
                        break;
                    case ServiceTypes.FileServer:
                        deviceConfig = new FileServerConfig
                        {
                            Id = TextBox_Code.Text,
                            Name = TextBox_Name.Text,
                            Endpoint = "tcp://127.0.0.1:" + (Math.Abs(new Random().Next()) % 99 + 6500),
                            FileBasePath = "D:\\CVTest",
                        };
                        sysResourceModel = saveConfigInfo(deviceConfig, sysResource);
                        if (sysResourceModel != null)
                            serviceTerminal.AddChild(new DeviceFileServer(sysResourceModel));
                        break;
                    case ServiceTypes.Algorithm:
                        deviceConfig = new ConfigAlgorithm
                        {
                            Id = TextBox_Code.Text,
                            Name = TextBox_Name.Text,
                            FileServerCfg = new FileServerCfg()
                            {
                                Endpoint = "tcp://127.0.0.1:" + (Math.Abs(new Random().Next()) % 99 + 6600),
                                DataBasePath = "D:\\CVTest",
                            }
                        };
                        sysResourceModel = saveConfigInfo(deviceConfig, sysResource);
                        if (sysResourceModel != null)
                            serviceTerminal.AddChild(new DeviceAlgorithm(sysResourceModel));
                        break;
                    case ServiceTypes.CfwPort:
                        deviceConfig = new ConfigCfwPort { 
                            Id = TextBox_Code.Text,
                            Name = TextBox_Name.Text,
                        };
                        sysResourceModel = saveConfigInfo(deviceConfig, sysResource);
                        if (sysResourceModel != null)
                            serviceTerminal.AddChild(new DeviceCfwPort(sysResourceModel));
                        break;
                    case ServiceTypes.Calibration:
                        deviceConfig = new ConfigCalibration
                        {
                            Id = TextBox_Code.Text,
                            Name = TextBox_Name.Text,
                        };
                        sysResourceModel = saveConfigInfo(deviceConfig, sysResource);
                        if (sysResourceModel != null)
                            serviceTerminal.AddChild(new DeviceCalibration(sysResourceModel));
                        break;
                    case ServiceTypes.Motor:
                        deviceConfig = new ConfigMotor
                        {
                            Id = TextBox_Code.Text,
                            Name = TextBox_Name.Text,
                        };
                        sysResourceModel = saveConfigInfo(deviceConfig, sysResource);
                        if (sysResourceModel != null)
                            serviceTerminal.AddChild(new DeviceMotor(sysResourceModel));
                        break;
                    default:
                        break;
                };
                MessageBox.Show("添加资源成功");
                MQTTCreate.Visibility = Visibility.Collapsed;
            }

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ServiceTerminal.Config.SubscribeTopic = ServiceTerminal.SysResourceModel.TypeCode + "/STATUS/" + ServiceTerminal.SysResourceModel.Code;
            ServiceTerminal.Config.SendTopic = ServiceTerminal.SysResourceModel.TypeCode + "/CMD/" + ServiceTerminal.SysResourceModel.Code;

            foreach (var item in ServiceTerminal.VisualChildren)
            {
                if(item is DeviceService mQTTDevice)
                {
                    mQTTDevice.SendTopic = ServiceTerminal.Config.SendTopic;
                    mQTTDevice.SubscribeTopic = ServiceTerminal.Config.SubscribeTopic;
                    mQTTDevice.Save();
                }
            }
            ServiceTerminal.Save();

            MQTTEditContent.Visibility = Visibility.Collapsed;
            MQTTShowContent.Visibility = Visibility.Visible;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MQTTShowContent.Visibility = Visibility.Collapsed;
            MQTTEditContent.Visibility = Visibility.Visible;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            MQTTCreate.Visibility = MQTTCreate.Visibility == Visibility.Visible? Visibility.Collapsed : Visibility.Visible;
            if (ServiceTerminal.MQTTServiceTerminalBase is MQTTServiceTerminalBase baseServiceBase)
            {
                TextBox_Code.ItemsSource = baseServiceBase.DevicesSN;
                TextBox_Name.ItemsSource = baseServiceBase.DevicesSN;
            }
        }


        private void ListViewService_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                if (ServiceTerminal.VisualChildren[listView.SelectedIndex] is DeviceService baseObject)
                {
                    if (this.Parent is Grid grid)
                    {
                        grid.Children.Clear();
                        grid.Children.Add(baseObject.GetDeviceControl());
                    }

                }
            }
        }


        private void ReFresh_Click(object sender, RoutedEventArgs e)
        {

        }

        private void TextBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            popup.IsOpen = true;
        }

        private void popup_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Popup popup)
                popup.IsOpen = false;

        }
    }
}
