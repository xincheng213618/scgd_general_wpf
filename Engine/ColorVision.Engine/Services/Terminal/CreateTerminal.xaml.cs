using ColorVision.Database;
using ColorVision.Engine.Rbac;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.FileServer;
using ColorVision.Engine.Services.Devices.FlowDevice;
using ColorVision.Engine.Services.Devices.Motor;
using ColorVision.Engine.Services.Devices.PG;
using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms;
using ColorVision.Engine.Services.Types;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Terminal
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
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            CreateCode.Text = TerminalService.NewCreateFileName($"DEV.{TerminalService.ServiceType}.Default");
            CreateName.Text = TerminalService.NewCreateFileName($"DEV.{TerminalService.ServiceType}.Default");

            DataContext = TerminalService;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SysResourceModel? saveDevConfigInfo(DeviceServiceConfig deviceConfig, SysResourceModel sysResource)
            {
                deviceConfig.Name = CreateCode.Text;
                deviceConfig.Code = CreateName.Text;

                deviceConfig.SendTopic = TerminalService.Config.SendTopic;
                deviceConfig.SubscribeTopic = TerminalService.Config.SubscribeTopic;
                sysResource.Value = JsonConvert.SerializeObject(deviceConfig);
                int pkId = MySqlControl.GetInstance().DB.Insertable(sysResource).ExecuteReturnIdentity();
                sysResource.Id = pkId;
                return sysResource;
            }


            var deviceS = ServiceManager.GetInstance().DeviceServices.FirstOrDefault(x => x.Code == CreateCode.Text);
            if (deviceS != null)
            {
                MessageBox1.Show(WindowHelpers.GetActiveWindow(), "设备标识已存在,不允许重复添加","ColorVision");
                return;
            }
            DeviceService deviceService = null;


            SysResourceModel sysResource = new SysResourceModel();
            sysResource.Name = CreateName.Text;
            sysResource.Code = CreateCode.Text;
            sysResource.Type = TerminalService.SysResourceModel.Type;
            sysResource.Pid = TerminalService.SysResourceModel.Id;
            sysResource.TenantId = UserConfig.Instance.TenantId;


            SysResourceModel sysDevModel = null;
            DeviceServiceConfig deviceConfig;
            int fromPort;
            switch (TerminalService.ServiceType)
            {
                case ServiceTypes.Camera:
                    ConfigCamera configCamera = new ConfigCamera()
                    {
                        Id = CreateCode.Text,
                        Name = CreateName.Text,
                    };
                    sysDevModel = saveDevConfigInfo(configCamera, sysResource);
                    if (sysDevModel != null)
                    {
                        deviceService = new DeviceCamera(sysDevModel);
                    }
                    break;
                case ServiceTypes.PG:
                    ConfigPG pGConfig = new()
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
                        IsCCTWave = true,
                    };
                    sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                    if (sysDevModel != null)
                    {
                        deviceService = new DeviceAlgorithm(sysDevModel);
                    }
                    break;
                case ServiceTypes.FilterWheel:
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
                case ServiceTypes.ThirdPartyAlgorithms:
                    deviceConfig = new ConfigThirdPartyAlgorithms
                    {
                        Id = CreateCode.Text,
                        Name = CreateName.Text,
                    };
                    sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                    if (sysDevModel != null)
                    {
                        deviceService = new DeviceThirdPartyAlgorithms(sysDevModel);
                    }
                    break;
                case ServiceTypes.Flow:
                    deviceConfig = new ConfigFlowDevice
                    {
                        Id = CreateCode.Text,
                        Name = CreateName.Text,
                    };
                    sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                    if (sysDevModel != null)
                    {
                        deviceService = new DeviceFlowDevice(sysDevModel);
                    }

                    break;
                default:
                    break;
            };
            if (deviceService != null)
            {
                TerminalService.AddChild(deviceService);
                ServiceManager.GetInstance().DeviceServices.Add(deviceService);

                string TypeCode = MySqlControl.GetInstance().DB.Queryable<SysDictionaryModel>().Where(x => x.Pid == 1 && x.Value == sysDevModel.Pid).First().Key;
                string PCode = MySqlControl.GetInstance().DB.Queryable<SysResourceModel>().InSingle(sysDevModel.Type).Code;

                RC.MqttRCService.GetInstance().RestartServices(TypeCode, PCode, sysDevModel.Code);
                Close();
            }
            else
            {
                MessageBox1.Show(WindowHelpers.GetActiveWindow(), "创建失败", "ColorVision");
            }

        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }
    }
}
