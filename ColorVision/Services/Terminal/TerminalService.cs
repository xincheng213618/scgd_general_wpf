using ColorVision.Common.MVVM;
using ColorVision.Services.RC;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices;
using ColorVision.Services.Extension;
using ColorVision.Services.Type;
using ColorVision.Common.Utilities;
using Newtonsoft.Json;
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
        private bool _IsChecked;
        public ContextMenu ContextMenu { get; set; }


        public virtual UserControl GenDeviceControl()
        {
            throw new System.NotImplementedException();
        }
    }

    public class TerminalService : TerminalServiceBase, IIcon
    {
        public SysResourceModel SysResourceModel { get; set; }
        public TerminalServiceConfig Config { get; set; }

        public MQTTServiceTerminalBase MQTTServiceTerminalBase { get; set; }

        public ServiceTypes ServiceType { get => (ServiceTypes)SysResourceModel.Type; }

        public override string Name { get => SysResourceModel.Name??string.Empty ; set { SysResourceModel.Name = value; NotifyPropertyChanged(); } }

        public string Code { get => SysResourceModel.Code ?? string.Empty; set { SysResourceModel.Code = value; NotifyPropertyChanged(); } }

        public ImageSource Icon { get; set; }

        public RelayCommand RefreshCommand { get; set; }
        public RelayCommand EditCommand { get; set; }
        public RelayCommand OpenCreateWindowCommand { get; set; }
        public TerminalService(SysResourceModel sysResourceModel) : base()
        {
            SysResourceModel = sysResourceModel;
            Config = BaseResourceObjectExtensions.TryDeserializeConfig<TerminalServiceConfig>(SysResourceModel.Value);

            Config.Code = Code;
            Config.Name = Name;

            RefreshCommand = new RelayCommand(a => MQTTRCService.GetInstance().RestartServices(Config.ServiceType.ToString(),sysResourceModel.Code ??string.Empty));
            EditCommand = new RelayCommand(a =>
            {
                EditTerminal window = new EditTerminal(this);
                window.Owner = WindowHelpers.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            });

            OpenCreateWindowCommand = new RelayCommand(a =>
            {
                CreateTerminal createTerminal = new CreateTerminal(this);
                createTerminal.Owner = WindowHelpers.GetActiveWindow();
                createTerminal.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                createTerminal.ShowDialog();
            });

            switch (ServiceType)
            {
                case ServiceTypes.Camera:
                    MQTTServiceTerminalBase = new MQTTServiceTerminalBase<TerminalServiceConfig>(Config);
                    break;
                case ServiceTypes.Algorithm:
                    this.SetIconResource("DrawingImageAlgorithm");
                    MQTTServiceTerminalBase = new MQTTServiceTerminalBase<TerminalServiceConfig>(Config);
                    break;
                case ServiceTypes.SMU:
                    this.SetIconResource("SMUDrawingImage");
                    MQTTServiceTerminalBase = new MQTTServiceTerminalBase<TerminalServiceConfig>(Config);
                    break;
                case ServiceTypes.Motor:
                    this.SetIconResource("COMDrawingImage");
                    MQTTServiceTerminalBase = new MQTTServiceTerminalBase<TerminalServiceConfig>(Config);
                    break;
                case ServiceTypes.CfwPort:
                    this.SetIconResource("CfwPortDrawingImage");
                    MQTTServiceTerminalBase = new MQTTServiceTerminalBase<TerminalServiceConfig>(Config);
                    break;
                case ServiceTypes.Calibration:
                    this.SetIconResource("DICalibrationIcon");
                    MQTTServiceTerminalBase = new MQTTServiceTerminalBase<TerminalServiceConfig>(Config);
                    break;
                case ServiceTypes.Spectrum:
                    this.SetIconResource("DISpectrumIcon");
                    MQTTServiceTerminalBase = new MQTTServiceTerminalBase<TerminalServiceConfig>(Config);
                    break;
                default:
                    MQTTServiceTerminalBase = new MQTTServiceTerminalBase<TerminalServiceConfig>(Config);
                    break;
            }

            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "删除服务" };
            menuItem.Click += (s, e) => Delete();
            ContextMenu.Items.Add(menuItem);
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
            SysResourceModel.Name = Config.Name;
            SysResourceModel.Code = Config.Code;
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            ServiceManager.GetInstance().VSysResourceDao.Save(SysResourceModel);
           
            MQTTRCService.GetInstance().RestartServices(Config.ServiceType.ToString());
        }
    }
}
