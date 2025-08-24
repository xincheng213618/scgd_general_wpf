using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Types;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using Newtonsoft.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Services.Terminal
{
    public class TerminalServiceBase : ServiceObjectBase, ITreeViewItem
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


            Config = ServiceObjectBaseExtensions.TryDeserializeConfig<TerminalServiceConfig>(SysResourceModel.Value);

            Config.Code = Code;
            Config.Name = Name;

            RefreshCommand = new RelayCommand(a => MqttRCService.GetInstance().RestartServices(Config.ServiceType.ToString(),sysResourceModel.Code ??string.Empty));
            EditCommand = new RelayCommand(a =>
            {
                PropertyEditorWindow window = new PropertyEditorWindow(Config);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();


            }, a => AccessControl.Check(PermissionMode.Administrator));

            OpenCreateWindowCommand = new RelayCommand(a =>
            {
                CreateTerminal createTerminal = new(this);
                createTerminal.Owner = Application.Current.GetActiveWindow();
                createTerminal.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                createTerminal.ShowDialog();
            }, a => AccessControl.Check(PermissionMode.Administrator));

            switch (ServiceType)
            {
                case ServiceTypes.Camera:
                    break;
                case ServiceTypes.Algorithm:
                    this.SetIconResource("DrawingImageAlgorithm");
                    break;
                case ServiceTypes.SMU:
                    this.SetIconResource("SMUDrawingImage");
                    break;
                case ServiceTypes.Motor:
                    this.SetIconResource("COMDrawingImage");
                    break;
                case ServiceTypes.FilterWheel:
                    this.SetIconResource("CfwPortDrawingImage");
                    break;
                case ServiceTypes.Calibration:
                    this.SetIconResource("DICalibrationIcon");
                    break;
                case ServiceTypes.Spectrum:
                    this.SetIconResource("DISpectrumIcon");
                    break;
                default:
                    break;
            }
            MQTTServiceTerminalBase = new MQTTServiceTerminalBase<TerminalServiceConfig>(Config);

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Create, Command = OpenCreateWindowCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.MenuEdit, Command = EditCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.MenuDelete, Command = DeleteCommand });
        }

        public override void Delete()
        {
            if (MessageBox1.Show(Application.Current.GetActiveWindow(),"非必要情况下请勿删除,是否删除", "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.Cancel) return;
            base.Delete();
            Parent.RemoveChild(this);
            if (SysResourceModel != null)
            {

                MySqlControl.GetInstance().DB.Deleteable<SysResourceModel>().Where(x => x.Pid == SysResourceModel.Id).ExecuteCommand();
                MySqlControl.GetInstance().DB.Deleteable<SysResourceModel>().Where(x => x.Id == SysResourceModel.Id).ExecuteCommand();

            }
            ServiceManager.GetInstance().TerminalServices.Remove(this);
        }


        public override UserControl GenDeviceControl() => new TerminalServiceControl(this);

        public override void Save()
        {
            base.Save();
            SysResourceModel.Name = Config.Name;
            SysResourceModel.Code = Config.Code;
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            MySqlControl.GetInstance().DB.Updateable<SysResourceModel>().ExecuteCommand();
            MqttRCService.GetInstance().RestartServices(Config.ServiceType.ToString());
        }
    }
}
