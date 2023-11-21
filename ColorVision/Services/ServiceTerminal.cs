using ColorVision.Device;
using ColorVision.Device.Camera;
using ColorVision.MQTT;
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.Themes;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Services
{
    public class BaseServiceTerminal : BaseObject
    {
        public virtual UserControl GenDeviceControl()
        {
            throw new System.NotImplementedException();
        }
    }

    public class ServiceTerminal : BaseServiceTerminal
    {
        public SysResourceModel SysResourceModel { get; set; }
        public BaseServiceConfig Config { get; set; }
        public BaseServiceBase BaseService { get; set; }

        public ServiceType ServiceType { get => (ServiceType)SysResourceModel.Type; }

        public override string Name { get => SysResourceModel.Name??string.Empty ; set { SysResourceModel.Name = value; NotifyPropertyChanged(); } }

        public string Code { get => SysResourceModel.Code ?? string.Empty; set { SysResourceModel.Code = value; NotifyPropertyChanged(); } }

        public ImageSource Icon { get; set; }

        public RelayCommand RefreshCommand { get; set; }

        public ServiceTerminal(SysResourceModel sysResourceModel) : base()
        {
            SysResourceModel = sysResourceModel;
            if (string.IsNullOrEmpty(SysResourceModel.Value))
            {
                Config ??= new BaseServiceConfig();
            }
            else
            {
                try
                {
                    Config = JsonConvert.DeserializeObject<BaseServiceConfig>(SysResourceModel.Value) ?? new BaseServiceConfig();
                }
                catch
                {
                    Config = new BaseServiceConfig();
                }
            }
            Config.Code = SysResourceModel.Code ?? string.Empty;
            Config.Name = Name;
            Config.SubscribeTopic = SysResourceModel.TypeCode + "/STATUS/" + SysResourceModel.Code;
            Config.SendTopic = SysResourceModel.TypeCode + "/CMD/" + SysResourceModel.Code;

            Config.IsAlive = false;


            switch (ServiceType)
            {
                case ServiceType.Camera:
                    ServiceCamera cameraService = new ServiceCamera(Config);
                    BaseService = cameraService;
                    RefreshCommand = new RelayCommand(a => cameraService.GetAllDevice());

                    if (Application.Current.TryFindResource("DrawingImageCamera") is DrawingImage DrawingImageCamera)
                        Icon = DrawingImageCamera;
                    ThemeManager.Current.CurrentUIThemeChanged += (s) =>
                    {
                        if (Application.Current.TryFindResource("DrawingImageCamera") is DrawingImage drawingImage)
                            Icon = drawingImage;
                    };

                    break;
                case ServiceType.Algorithm:
                    if (Application.Current.TryFindResource("DrawingImageAlgorithm") is DrawingImage DrawingImageAlgorithm)
                        Icon = DrawingImageAlgorithm;
                    ThemeManager.Current.CurrentUIThemeChanged += (s) =>
                    {
                        if (Application.Current.TryFindResource("DrawingImageAlgorithm") is DrawingImage drawingImage)
                            Icon = drawingImage;
                    };
                    BaseService = new BaseService<BaseServiceConfig>(Config);
                    break;
                case ServiceType.SMU:
                    if (Application.Current.TryFindResource("SMUDrawingImage") is DrawingImage SMUDrawingImage)
                        Icon = SMUDrawingImage;
                    ThemeManager.Current.CurrentUIThemeChanged += (s) =>
                    {
                        if (Application.Current.TryFindResource("SMUDrawingImage") is DrawingImage drawingImage)
                            Icon = drawingImage;
                    };
                    BaseService = new BaseService<BaseServiceConfig>(Config);
                    break;
                case ServiceType.Motor:
                    if (Application.Current.TryFindResource("COMDrawingImage") is DrawingImage COMDrawingImage)
                        Icon = COMDrawingImage;
                    ThemeManager.Current.CurrentUIThemeChanged += (s) =>
                    {
                        if (Application.Current.TryFindResource("COMDrawingImage") is DrawingImage drawingImage)
                            Icon = drawingImage;
                    };
                    BaseService = new BaseService<BaseServiceConfig>(Config);
                    break;
                case ServiceType.CfwPort:
                    if (Application.Current.TryFindResource("CfwPortDrawingImage") is DrawingImage CfwPortDrawingImage)
                        Icon = CfwPortDrawingImage;
                    ThemeManager.Current.CurrentUIThemeChanged += (s) =>
                    {
                        if (Application.Current.TryFindResource("CfwPortDrawingImage") is DrawingImage drawingImage)
                            Icon = drawingImage;
                    };
                    BaseService = new BaseService<BaseServiceConfig>(Config);
                    break;

                default:
                    BaseService = new BaseService<BaseServiceConfig>(Config);
                    break;
            }

            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "删除服务" };
            menuItem.Click += (s, e) =>
            {
                Parent.RemoveChild(this);
                if (SysResourceModel != null)
                {
                    ServiceManager.GetInstance().ResourceService.DeleteById(SysResourceModel.Id);
                    ServiceManager.GetInstance().ResourceService.DeleteAllByPid(SysResourceModel.Id);
                }
            };
            ContextMenu.Items.Add(menuItem);
        }

        public ServiceType Type { get => (ServiceType)SysResourceModel.Type; }

        public List<string> ServicesCodes
        {
            get
            {
                List<string> codes = new List<string>();
                foreach (var item in VisualChildren)
                {
                    if (item is BaseChannel baseChannel)
                    {
                        if (!string.IsNullOrWhiteSpace(baseChannel.SysResourceModel.Code))
                            codes.Add(baseChannel.SysResourceModel.Code);
                    }
                }
                return codes;
            }
        }




        public override UserControl GenDeviceControl() => new ServiceTerminalControl(this);

        public override void Save()
        {
            base.Save();
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            ServiceManager.GetInstance().ResourceService.Save(SysResourceModel);
        }
    }
}
