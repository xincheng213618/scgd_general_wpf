using ColorVision.Common.MVVM;
using ColorVision.RC;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.PG;
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
using ColorVision.Services.Extension;
using ColorVision.Services.Type;
using ColorVision.Settings;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ColorVision.Utilities;

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
        public RelayCommand CreateCommand { get; set; }
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
