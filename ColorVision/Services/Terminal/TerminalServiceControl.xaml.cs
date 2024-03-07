using ColorVision.Device.PG;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices;
using ColorVision.Services.Devices.Algorithm;
using ColorVision.Services.Devices.Calibration;
using ColorVision.Services.Devices.Camera;
using ColorVision.Services.Devices.Camera.Configs;
using ColorVision.Services.Devices.CfwPort;
using ColorVision.Services.Devices.FileServer;
using ColorVision.Services.Devices.Motor;
using ColorVision.Services.Devices.Sensor;
using ColorVision.Services.Devices.SMU;
using ColorVision.Services.Devices.SMU.Configs;
using ColorVision.Services.Devices.Spectrum;
using ColorVision.Services.Devices.Spectrum.Configs;
using ColorVision.Services.Type;
using ColorVision.Settings;
using cvColorVision;
using Newtonsoft.Json;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;


namespace ColorVision.Services.Terminal
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
        private SysDeviceModel? saveDevConfigInfo(DeviceServiceConfig deviceConfig, SysResourceModel sysResource)
        {
            deviceConfig.Name = TextBox_Name.Text;
            deviceConfig.Code = TextBox_Code.Text;

            deviceConfig.SendTopic = ServiceTerminal.Config.SendTopic;
            deviceConfig.SubscribeTopic = ServiceTerminal.Config.SubscribeTopic;
            sysResource.Value = JsonConvert.SerializeObject(deviceConfig);
            ServiceControl.VSysResourceDao.Save(sysResource);
            int pkId = sysResource.PKId;
            if (pkId > 0 && ServiceControl.VSysDeviceDao.GetById(pkId) is SysDeviceModel model) return model;
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
                SysDeviceModel sysDevModel = null;
                DeviceServiceConfig deviceConfig;
                int fromPort;
                switch (serviceTerminal.Type)
                {
                    case ServiceTypes.camera:
                        fromPort = (Math.Abs(new Random().Next()) % 99 + 6800);
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
                                Endpoint = "127.0.0.1",
                                PortRange = string.Format("{0}-{1}", fromPort, fromPort + 5),
                                DataBasePath = "D:\\CVTest",
                            },
                            VideoConfig = new Devices.Camera.Video.CameraVideoConfig()
                            {
                                Host = "127.0.0.1",
                                Port = (Math.Abs(new Random().Next()) % 99 + 9000),
                            }
                        };

                        sysDevModel = saveDevConfigInfo(cameraConfig1, sysResource);
                        if (sysDevModel != null)
                        {
                            if (serviceTerminal.MQTTServiceTerminalBase is MQTTTerminalCamera cameraService)
                            {
                                serviceTerminal.AddChild(new DeviceCamera(sysDevModel, cameraService));
                            }
                        }
                        break;
                    case ServiceTypes.pg:
                        ConfigPG pGConfig = new ConfigPG
                        {
                            Id = TextBox_Code.Text,
                            Name = TextBox_Name.Text
                        };
                        sysDevModel = saveDevConfigInfo(pGConfig, sysResource);
                        if (sysDevModel != null)
                            serviceTerminal.AddChild(new DevicePG(sysDevModel));
                        break;
                    case ServiceTypes.Spectrum:
                        fromPort = (Math.Abs(new Random().Next()) % 99 + 6700);
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
                            FileServerCfg = new FileServerCfg()
                            {
                                Endpoint = "127.0.0.1",
                                PortRange = string.Format("{0}-{1}", fromPort, fromPort + 5),
                                DataBasePath = "D:\\CVTest",
                            }
                        };
                        sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                        if (sysDevModel != null)
                            serviceTerminal.AddChild(new DeviceSpectrum(sysDevModel));
                        break;
                    case ServiceTypes.SMU:
                        deviceConfig = new ConfigSMU
                        {
                            Id = TextBox_Code.Text,
                            Name = TextBox_Name.Text
                        };
                        sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                        if (sysDevModel != null)
                            serviceTerminal.AddChild(new DeviceSMU(sysDevModel));
                        break;
                    case ServiceTypes.Sensor:
                        deviceConfig = new ConfigSensor
                        {
                            Id = TextBox_Code.Text,
                            Name = TextBox_Name.Text
                        };
                        sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                        if (sysDevModel != null)
                            serviceTerminal.AddChild(new DeviceSensor(sysDevModel));
                        break;
                    case ServiceTypes.FileServer:
                        fromPort = (Math.Abs(new Random().Next()) % 99 + 6500);
                        deviceConfig = new FileServerConfig
                        {
                            Id = TextBox_Code.Text,
                            Name = TextBox_Name.Text,
                            Endpoint = "127.0.0.1" ,
                            PortRange = string.Format("{0}-{1}", fromPort, fromPort+5),
                            FileBasePath = "D:\\CVTest",
                        };
                        sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                        if (sysDevModel != null)
                            serviceTerminal.AddChild(new DeviceFileServer(sysDevModel));
                        break;
                    case ServiceTypes.Algorithm:
                        fromPort = (Math.Abs(new Random().Next()) % 99 + 6600);
                        deviceConfig = new ConfigAlgorithm
                        {
                            Id = TextBox_Code.Text,
                            Name = TextBox_Name.Text,
                            FileServerCfg = new FileServerCfg()
                            {
                                Endpoint = "127.0.0.1",
                                PortRange = string.Format("{0}-{1}", fromPort, fromPort + 5),
                                DataBasePath = "D:\\CVTest",
                            }
                        };
                        sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                        if (sysDevModel != null)
                            serviceTerminal.AddChild(new DeviceAlgorithm(sysDevModel));
                        break;
                    case ServiceTypes.CfwPort:
                        deviceConfig = new ConfigCfwPort { 
                            Id = TextBox_Code.Text,
                            Name = TextBox_Name.Text,
                        };
                        sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                        if (sysDevModel != null)
                            serviceTerminal.AddChild(new DeviceCfwPort(sysDevModel));
                        break;
                    case ServiceTypes.Calibration:
                        fromPort = (Math.Abs(new Random().Next()) % 99 + 6200);
                        deviceConfig = new ConfigCalibration
                        {
                            Id = TextBox_Code.Text,
                            Name = TextBox_Name.Text,
                            FileServerCfg = new FileServerCfg()
                            {
                                Endpoint = "127.0.0.1",
                                PortRange = string.Format("{0}-{1}", fromPort, fromPort + 5),
                                DataBasePath = "D:\\CVTest",
                            }
                        };
                        sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                        if (sysDevModel != null)
                            serviceTerminal.AddChild(new DeviceCalibration(sysDevModel));
                        break;
                    case ServiceTypes.Motor:
                        deviceConfig = new ConfigMotor
                        {
                            Id = TextBox_Code.Text,
                            Name = TextBox_Name.Text,
                        };
                        sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                        if (sysDevModel != null)
                            serviceTerminal.AddChild(new DeviceMotor(sysDevModel));
                        break;
                    default:
                        break;
                };
                if (sysDevModel != null && sysDevModel.TypeCode!=null && sysDevModel.PCode!=null && sysDevModel.Code!=null) RC.MQTTRCService.GetInstance().RestartServices(sysDevModel.TypeCode, sysDevModel.PCode, sysDevModel.Code);
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
