using ColorVision.Common.MVVM;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Algorithm;
using ColorVision.Services.Devices.Calibration;
using ColorVision.Services.Devices.Camera;
using ColorVision.Services.Devices;
using ColorVision.Services.Devices.Camera.Configs;
using ColorVision.Services.Devices.CfwPort;
using ColorVision.Services.Devices.FileServer;
using ColorVision.Services.Devices.Motor;
using ColorVision.Services.Devices.PG;
using ColorVision.Services.Devices.Sensor;
using ColorVision.Services.Devices.SMU.Configs;
using ColorVision.Services.Devices.SMU;
using ColorVision.Services.Devices.Spectrum.Configs;
using ColorVision.Services.Devices.Spectrum;
using ColorVision.Services.Type;
using ColorVision.Settings;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ColorVision.Common.Utilities;

namespace ColorVision.Services.Terminal
{
    /// <summary>
    /// EditTerminal.xaml 的交互逻辑
    /// </summary>
    public partial class CreateTerminal : Window
    {
        public TerminalService TerminalService { get; set; }
        public CreateTerminal(TerminalService terminalService)
        {
            TerminalService = terminalService;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            CreateCode.Text = TerminalService.NewCreateFileName($"DEV.{TerminalService.ServiceType}.Default");
            CreateName.Text = TerminalService.NewCreateFileName($"DEV.{TerminalService.ServiceType}.Default");

            this.DataContext = TerminalService;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SysDeviceModel? saveDevConfigInfo(DeviceServiceConfig deviceConfig, SysResourceModel sysResource)
            {
                deviceConfig.Name = CreateCode.Text;
                deviceConfig.Code = CreateName.Text;

                deviceConfig.SendTopic = TerminalService.Config.SendTopic;
                deviceConfig.SubscribeTopic = TerminalService.Config.SubscribeTopic;
                sysResource.Value = JsonConvert.SerializeObject(deviceConfig);
                ServiceManager.GetInstance().VSysResourceDao.Save(sysResource);
                int pkId = sysResource.PKId;
                if (pkId > 0 && ServiceManager.GetInstance().VSysDeviceDao.GetById(pkId) is SysDeviceModel model) return model;
                else return null;
            }


            var deviceS = ServiceManager.GetInstance().DeviceServices.FirstOrDefault(x => x.Code == CreateCode.Text);
            if (deviceS != null)
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "设备标识已存在,不允许重复添加","ColorVision");
                return;
            }
            DeviceService deviceService = null;


            SysResourceModel sysResource = new SysResourceModel(CreateName.Text, CreateCode.Text, TerminalService.SysResourceModel.Type, TerminalService.SysResourceModel.Id, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            SysDeviceModel sysDevModel = null;
            DeviceServiceConfig deviceConfig;
            int fromPort;
            switch (TerminalService.ServiceType)
            {
                case ServiceTypes.PG:
                    ConfigPG pGConfig = new ConfigPG
                    {
                        Id = CreateCode.Text,
                        Name = CreateName.Text,
                    };
                    sysDevModel = saveDevConfigInfo(pGConfig, sysResource);
                    if (sysDevModel != null)
                    {
                        deviceService = new DevicePG(sysDevModel);
                    }
                    break;
                case ServiceTypes.Spectrum:
                    fromPort = (Math.Abs(new Random().Next()) % 99 + 6700);
                    deviceConfig = new ConfigSpectrum
                    {
                        Id = CreateCode.Text,
                        Name = CreateName.Text,
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
                    {
                        deviceService = new DeviceSpectrum(sysDevModel);
                    }
                    break;
                case ServiceTypes.SMU:
                    deviceConfig = new ConfigSMU
                    {
                        Id = CreateCode.Text,
                        Name = CreateName.Text,
                    };
                    sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                    if (sysDevModel != null)
                    {
                        deviceService = new DeviceSMU(sysDevModel);
                    }
                    break;
                case ServiceTypes.Sensor:
                    deviceConfig = new ConfigSensor
                    {
                        Id = CreateCode.Text,
                        Name = CreateName.Text,
                    };
                    sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                    if (sysDevModel != null)
                    {
                        deviceService = new DeviceSensor(sysDevModel);
                    }
                    break;
                case ServiceTypes.FileServer:
                    fromPort = (Math.Abs(new Random().Next()) % 99 + 6500);
                    deviceConfig = new ConfigFileServer
                    {
                        Id = CreateCode.Text,
                        Name = CreateName.Text,
                        Endpoint = "127.0.0.1",
                        PortRange = string.Format("{0}-{1}", fromPort, fromPort + 5),
                        FileBasePath = "D:\\CVTest",
                    };
                    sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                    if (sysDevModel != null)
                    {
                        deviceService = new DeviceFileServer(sysDevModel);
                    }

                    break;
                case ServiceTypes.Algorithm:
                    fromPort = (Math.Abs(new Random().Next()) % 99 + 6600);
                    deviceConfig = new ConfigAlgorithm
                    {
                        Id = CreateCode.Text,
                        Name = CreateName.Text,
                        FileServerCfg = new FileServerCfg()
                        {
                            Endpoint = "127.0.0.1",
                            PortRange = string.Format("{0}-{1}", fromPort, fromPort + 5),
                            DataBasePath = "D:\\CVTest",
                        }
                    };
                    sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                    if (sysDevModel != null)
                    {
                        deviceService = new DeviceAlgorithm(sysDevModel);
                    }
                    break;
                case ServiceTypes.CfwPort:
                    deviceConfig = new ConfigCfwPort
                    {
                        Id = CreateCode.Text,
                        Name = CreateName.Text,
                    };
                    sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                    if (sysDevModel != null)
                    {
                        deviceService = new DeviceCfwPort(sysDevModel);
                    }
                    break;
                case ServiceTypes.Calibration:
                    fromPort = (Math.Abs(new Random().Next()) % 99 + 6200);
                    deviceConfig = new ConfigCalibration
                    {
                        Id = CreateCode.Text,
                        Name = CreateName.Text,
                        FileServerCfg = new FileServerCfg()
                        {
                            Endpoint = "127.0.0.1",
                            PortRange = string.Format("{0}-{1}", fromPort, fromPort + 5),
                            DataBasePath = "D:\\CVTest",
                        }
                    };
                    sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                    if (sysDevModel != null)
                    {
                        deviceService = new DeviceCalibration(sysDevModel);
                    }
                    break;
                case ServiceTypes.Motor:
                    deviceConfig = new ConfigMotor
                    {
                        Id = CreateCode.Text,
                        Name = CreateName.Text,
                    };
                    sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                    if (sysDevModel != null)
                    {
                        deviceService = new DeviceMotor(sysDevModel);
                    }
                    break;
                default:
                    break;
            };
            if (deviceService != null)
            {
                TerminalService.AddChild(deviceService);
                ServiceManager.GetInstance().DeviceServices.Add(deviceService);
                if (sysDevModel != null && sysDevModel.TypeCode != null && sysDevModel.PCode != null && sysDevModel.Code != null)
                    RC.MQTTRCService.GetInstance().RestartServices(sysDevModel.TypeCode, sysDevModel.PCode, sysDevModel.Code);
                //MessageBox.Show(WindowHelpers.GetActiveWindow(),"创建成功，正在重启服务", "ColorVision");
                this.Close();
            }
            else
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "创建失败", "ColorVision");
            }

        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }
    }
}
