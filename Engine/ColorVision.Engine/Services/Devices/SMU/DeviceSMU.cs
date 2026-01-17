using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Extension;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Views;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.SMU
{
    public class DisplaySMUConfig : IDisplayConfigBase
    {
        public bool IsUseLimitSigned { get => _IsUseLimitSigned; set { _IsUseLimitSigned = value; OnPropertyChanged(); } }
        private bool _IsUseLimitSigned = true;

        public double MeasureVal { get => _MeasureVal; set { _MeasureVal = value; OnPropertyChanged(); } }
        private double _MeasureVal = 5;

        public double LmtVal { get => _lmtVal; set { _lmtVal = value; OnPropertyChanged(); } }
        private double _lmtVal = 5;


        public bool IsSourceV { get => _IsSourceV; set { _IsSourceV = value; OnPropertyChanged(); } }
        private bool _IsSourceV = true;


        public double StartMeasureVal { get => _startMeasureVal; set { _startMeasureVal = value; OnPropertyChanged(); } }
        private double _startMeasureVal;
        public double StopMeasureVal { get => _stopMeasureVal; set { _stopMeasureVal = value; OnPropertyChanged(); } }
        private double _stopMeasureVal;

        public int Number { get => _number; set { _number = value; OnPropertyChanged(); } }
        private int _number;
        public double LimitVal { get => _limitVal; set { _limitVal = value; OnPropertyChanged(); } }
        private double _limitVal = 5;

        public SMUChannelType Channel { get => _Channel; set { _Channel = value; OnPropertyChanged(); } }
        private SMUChannelType _Channel = SMUChannelType.A;

        public double? V { get => _V; set { _V = value; OnPropertyChanged(); } }
        private double? _V;
        public double? I { get => _I; set { _I = value; OnPropertyChanged(); } }
        private double? _I;
    }

    public class DeviceSMU : DeviceService<ConfigSMU>
    {
        public MQTTSMU DService { get; set; }

        public ViewSMU View { get; set; }
        public DisplaySMUConfig DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<DisplaySMUConfig>(Config.Code);

        public DeviceSMU(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTSMU(this);
            View = new ViewSMU();
            View.View.Title = ColorVision.Engine.Properties.Resources.SMUView+$" - {Config.Code}";
            this.SetIconResource("SMUDrawingImage", View.View);


            EditCommand = new RelayCommand(a =>
            {
                EditSMU window = new(this);
                window.Icon = Icon;
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            }, a => AccessControl.Check(PermissionMode.Administrator));

            EditSMUTemplateCommand = new RelayCommand(a => EditSMUTemplate());

            EditDisplayConfigCommand =new RelayCommand(a => EditDisplayConfig());   
        }

        [CommandDisplay("编辑显示配置", Order = -1)]
        public RelayCommand EditDisplayConfigCommand { get; set; }
        public void EditDisplayConfig()
        {
            new PropertyEditorWindow(DisplayConfig) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }


        [CommandDisplay("MenuSUM",Order =100)]
        public RelayCommand EditSMUTemplateCommand { get; set; }

        public static void EditSMUTemplate()
        {

            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.DatabaseConnectionFailed, "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplateSMUParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }


        public override UserControl GetDeviceInfo() => new InfoSMU(this);
        public override UserControl GetDisplayControl() => new DisplaySMU(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
