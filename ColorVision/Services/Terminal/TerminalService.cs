using ColorVision.Device.PG;
using ColorVision.Common.MVVM;
using ColorVision.RC;
using ColorVision.Services.Core;
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
using ColorVision.Services.Devices.SMU.Configs;
using ColorVision.Services.Devices.SMU;
using ColorVision.Services.Devices.Spectrum.Configs;
using ColorVision.Services.Devices.Spectrum;
using ColorVision.Services.Type;
using ColorVision.Settings;
using ColorVision.Themes;
using cvColorVision;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Services.Terminal
{
    public class TerminalServiceBase : BaseResourceObject, ITreeViewItem
    {
        public bool IsExpanded { get => _IsExpanded; set { _IsExpanded = value; NotifyPropertyChanged(); } }
        private bool _IsExpanded = true;

        public bool IsSelected { get => _IsChecked; set { _IsChecked = value; NotifyPropertyChanged(); } }
        private bool _IsChecked = true;
        public ContextMenu ContextMenu { get; set; }


        public virtual UserControl GenDeviceControl()
        {
            throw new System.NotImplementedException();
        }
    }

    public class TerminalService : TerminalServiceBase
    {
        public SysResourceModel SysResourceModel { get; set; }
        public TerminalServiceConfig Config { get; set; }

        public MQTTServiceTerminalBase MQTTServiceTerminalBase { get; set; }

        public ServiceTypes ServiceType { get => (ServiceTypes)SysResourceModel.Type; }

        public override string Name { get => SysResourceModel.Name??string.Empty ; set { SysResourceModel.Name = value; NotifyPropertyChanged(); } }

        public string Code { get => SysResourceModel.Code ?? string.Empty; set { SysResourceModel.Code = value; NotifyPropertyChanged(); } }

        public ImageSource Icon { get; set; }

        public RelayCommand RefreshCommand { get; set; }
        public RelayCommand OpenCreateWindowCommand { get; set; }
        public RelayCommand CreateCommand { get; set; }
        public EventHandler CreateDeviceOver { get; set; }

        public TerminalService(SysResourceModel sysResourceModel) : base()
        {
            SysResourceModel = sysResourceModel;
            if (string.IsNullOrEmpty(SysResourceModel.Value))
            {
                Config ??= new TerminalServiceConfig();
            }
            else
            {
                try
                {
                    Config = JsonConvert.DeserializeObject<TerminalServiceConfig>(SysResourceModel.Value) ?? new TerminalServiceConfig();
                }
                catch
                {
                    Config = new TerminalServiceConfig();
                }
            }
            Config.Code = SysResourceModel.Code ?? string.Empty;
            Config.Name = Name;

            Config.SubscribeTopic = SysResourceModel.TypeCode + "/STATUS/" + SysResourceModel.Code;
            Config.SendTopic = SysResourceModel.TypeCode + "/CMD/" + SysResourceModel.Code;

            CreateCommand = new RelayCommand(a => Create());
            OpenCreateWindowCommand = new RelayCommand(a => OpenCreateWindow());
            switch (ServiceType)
            {
                case ServiceTypes.camera:
                    MQTTTerminalCamera cameraService = new MQTTTerminalCamera(Config);
                    MQTTServiceTerminalBase = cameraService;
                    RefreshCommand = new RelayCommand(a => cameraService.GetAllDevice());

                    if (Application.Current.TryFindResource("DrawingImageCamera") is DrawingImage DrawingImageCamera)
                        Icon = DrawingImageCamera;
                    ThemeManager.Current.CurrentUIThemeChanged += (s) =>
                    {
                        if (Application.Current.TryFindResource("DrawingImageCamera") is DrawingImage drawingImage)
                            Icon = drawingImage;
                    };

                    break;
                case ServiceTypes.Algorithm:
                    if (Application.Current.TryFindResource("DrawingImageAlgorithm") is DrawingImage DrawingImageAlgorithm)
                        Icon = DrawingImageAlgorithm;
                    ThemeManager.Current.CurrentUIThemeChanged += (s) =>
                    {
                        if (Application.Current.TryFindResource("DrawingImageAlgorithm") is DrawingImage drawingImage)
                            Icon = drawingImage;
                    };
                    MQTTServiceTerminalBase = new MQTTServiceTerminalBase<TerminalServiceConfig>(Config);
                    break;
                case ServiceTypes.SMU:
                    if (Application.Current.TryFindResource("SMUDrawingImage") is DrawingImage SMUDrawingImage)
                        Icon = SMUDrawingImage;
                    ThemeManager.Current.CurrentUIThemeChanged += (s) =>
                    {
                        if (Application.Current.TryFindResource("SMUDrawingImage") is DrawingImage drawingImage)
                            Icon = drawingImage;
                    };
                    MQTTServiceTerminalBase = new MQTTServiceTerminalBase<TerminalServiceConfig>(Config);
                    break;
                case ServiceTypes.Motor:
                    if (Application.Current.TryFindResource("COMDrawingImage") is DrawingImage COMDrawingImage)
                        Icon = COMDrawingImage;
                    ThemeManager.Current.CurrentUIThemeChanged += (s) =>
                    {
                        if (Application.Current.TryFindResource("COMDrawingImage") is DrawingImage drawingImage)
                            Icon = drawingImage;
                    };
                    MQTTServiceTerminalBase = new MQTTServiceTerminalBase<TerminalServiceConfig>(Config);
                    break;
                case ServiceTypes.CfwPort:
                    if (Application.Current.TryFindResource("CfwPortDrawingImage") is DrawingImage CfwPortDrawingImage)
                        Icon = CfwPortDrawingImage;
                    ThemeManager.Current.CurrentUIThemeChanged += (s) =>
                    {
                        if (Application.Current.TryFindResource("CfwPortDrawingImage") is DrawingImage drawingImage)
                            Icon = drawingImage;
                    };
                    MQTTServiceTerminalBase = new MQTTServiceTerminalBase<TerminalServiceConfig>(Config);
                    break;

                default:
                    MQTTServiceTerminalBase = new MQTTServiceTerminalBase<TerminalServiceConfig>(Config);
                    break;
            }

            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "删除服务" };
            menuItem.Click += (s, e) =>
            {
                Delete();
            };
            ContextMenu.Items.Add(menuItem);
        }
        public string CreatCode { get => _CreatCode; set { _CreatCode = value; NotifyPropertyChanged(); } }
        private string _CreatCode;
        public string CreatName { get => _CreatName; set { _CreatName = value; NotifyPropertyChanged(); } }
        private string _CreatName;

        public virtual void OpenCreateWindow()
        {

        }

        public virtual void Create()
        {

            SysDeviceModel? saveDevConfigInfo(DeviceServiceConfig deviceConfig, SysResourceModel sysResource)
            {
                deviceConfig.Name = CreatName;
                deviceConfig.Code = CreatCode;

                deviceConfig.SendTopic = Config.SendTopic;
                deviceConfig.SubscribeTopic = Config.SubscribeTopic;
                sysResource.Value = JsonConvert.SerializeObject(deviceConfig);
                ServiceManager.GetInstance().VSysResourceDao.Save(sysResource);
                int pkId = sysResource.PKId;
                if (pkId > 0 && ServiceManager.GetInstance().VSysDeviceDao.GetById(pkId) is SysDeviceModel model) return model;
                else return null;
            }


            if (!ServicesHelper.IsInvalidPath(CreatName, "资源名称") || !ServicesHelper.IsInvalidPath(CreatCode, "资源标识"))
                return;

            if (ServicesCodes.Contains(CreatCode))
            {
                MessageBox.Show("设备标识已存在,不允许重复添加");
                return;
            }
            DeviceService deviceService = null;


            SysResourceModel sysResource = new SysResourceModel(CreatName, CreatCode, SysResourceModel.Type, SysResourceModel.Id, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            SysDeviceModel sysDevModel = null;
            DeviceServiceConfig deviceConfig;
            int fromPort;
            switch (Type)
            {
                case ServiceTypes.camera:
                    //在拆干净之前先放在这里
                    TerminalCamera terminalCamera = new TerminalCamera(sysResource);
                    break;
                case ServiceTypes.pg:
                    ConfigPG pGConfig = new ConfigPG
                    {
                        Id = CreatCode,
                        Name = CreatName,
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
                        Id = CreatCode,
                        Name = CreatName,
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
                        Id = CreatCode,
                        Name = CreatName,
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
                        Id = CreatCode,
                        Name = CreatName,
                    };
                    sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
                    if (sysDevModel != null)
                        {
                        deviceService = new DeviceSensor(sysDevModel);
                    }
                    break;
                case ServiceTypes.FileServer:
                    fromPort = (Math.Abs(new Random().Next()) % 99 + 6500);
                    deviceConfig = new FileServerConfig
                    {
                        Id = CreatCode,
                        Name = CreatName,
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
                        Id = CreatCode,
                        Name = CreatName,
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
                        Id = CreatCode,
                        Name = CreatName,
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
                        Id = CreatCode,
                        Name = CreatName,
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
                        Id = CreatCode,
                        Name = CreatName,
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
                AddChild(deviceService);
                ServiceManager.GetInstance().DeviceServices.Add(deviceService);
                if (sysDevModel != null && sysDevModel.TypeCode != null && sysDevModel.PCode != null && sysDevModel.Code != null)
                    RC.MQTTRCService.GetInstance().RestartServices(sysDevModel.TypeCode, sysDevModel.PCode, sysDevModel.Code);
                MessageBox.Show("添加资源成功");
            }
            else
            {
                MessageBox.Show("资源创建失败");
            }

        }


        public override void Delete()
        {
            base.Delete();
            Parent.RemoveChild(this);
            if (SysResourceModel != null)
            {
                ServiceManager.GetInstance().VSysResourceDao.DeleteById(SysResourceModel.Id);
                ServiceManager.GetInstance().VSysResourceDao.DeleteAllByPid(SysResourceModel.Id);
            }

            ServiceManager.GetInstance().TerminalServices.Remove(this);
        }

        public ServiceTypes Type { get => (ServiceTypes)SysResourceModel.Type; }

        public List<string> ServicesCodes
        {
            get
            {
                List<string> codes = new List<string>();
                foreach (var item in VisualChildren)
                {
                    if (item is DeviceService baseChannel)
                    {
                        if (!string.IsNullOrWhiteSpace(baseChannel.SysResourceModel.Code))
                            codes.Add(baseChannel.SysResourceModel.Code);
                    }
                }
                return codes;
            }
        }

        public override UserControl GenDeviceControl() => new TerminalServiceControl(this);

        public override void Save()
        {
            base.Save();
            DBTerminalServiceConfig dbCfg = new DBTerminalServiceConfig { HeartbeatTime = Config.HeartbeatTime, };
            SysResourceModel.Value = JsonConvert.SerializeObject(dbCfg);
            ServiceManager.GetInstance().VSysResourceDao.Save(SysResourceModel);
           
            MQTTRCService.GetInstance().RestartServices(Config.ServiceType.ToString());
        }
    }
}
