using ColorVision.MVVM;
using ColorVision.RC;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices;
using ColorVision.Services.Devices.Camera;
using ColorVision.Themes;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Xps.Packaging;

namespace ColorVision.Services
{
    public class TerminalServiceBase : BaseResourceObject
    {
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
        public RelayCommand CreateCommand { get; set; }

        public virtual void Create()
        {
            MessageBox.Show("Create");
        }


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

        public override void Delete()
        {
            base.Delete();
            Parent.RemoveChild(this);
            if (SysResourceModel != null)
            {
                ServiceManager.GetInstance().ResourceService.DeleteById(SysResourceModel.Id);
                ServiceManager.GetInstance().ResourceService.DeleteAllByPid(SysResourceModel.Id);
            }
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
            //SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            ServiceManager.GetInstance().ResourceService.Save(SysResourceModel);
           
            MQTTRCService.GetInstance().RestartServices(Config.ServiceType.ToString());
        }
    }
}
