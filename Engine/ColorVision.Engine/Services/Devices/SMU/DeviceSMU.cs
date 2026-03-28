using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.UI.Extension;
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

        public double MeasureVal { get => _MeasureVal; set { 
                _MeasureVal = value; OnPropertyChanged();
                if (_Channel == SMUChannelType.A)
                    AMeasureVal = value;
                else if (_Channel == SMUChannelType.B)
                    BMeasureVal = value;
            } }
        private double _MeasureVal = 5;

        public double LmtVal { get => _lmtVal; set { 
                _lmtVal = value; OnPropertyChanged();
                if (_Channel == SMUChannelType.A)
                    ALmtVal = value;
                else if (_Channel == SMUChannelType.B)
                    BLmtVal = value;
            } }
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

        public SMUChannelType Channel { get => _Channel; set {
                _Channel = value; OnPropertyChanged(); 
                if (value == SMUChannelType.A)
                {
                    V = AV;
                    I = AI;
                    MeasureVal = AMeasureVal;
                    LmtVal = ALmtVal;
                }else if (value == SMUChannelType.B)
                {
                    V = BV;
                    I = BI;
                    MeasureVal = BMeasureVal;
                    LmtVal = BLmtVal;
                }
            } }
        private SMUChannelType _Channel = SMUChannelType.A;



        public double? AV { get => _AV; set { _AV = value; OnPropertyChanged(); } }
        private double? _AV;
        public double? AI { get => _AI; set { _AI = value; OnPropertyChanged(); } }
        private double? _AI;

        public double? BV { get => _BV; set { _BV = value; OnPropertyChanged(); } }
        private double? _BV;
        public double? BI { get => _BI; set { _BI = value; OnPropertyChanged(); } }
        private double? _BI;

        public double AMeasureVal { get => _AMeasureVal; set { _AMeasureVal = value; OnPropertyChanged(); } }
        private double _AMeasureVal = 5;
        public double ALmtVal { get => _ALmtVal; set { _ALmtVal = value; OnPropertyChanged(); } }
        private double _ALmtVal = 5;

        public double BMeasureVal { get => _BMeasureVal; set { _BMeasureVal = value; OnPropertyChanged(); } }
        private double _BMeasureVal = 5;
        public double BLmtVal { get => _BLmtVal; set { _BLmtVal = value; OnPropertyChanged(); } }
        private double _BLmtVal = 5;


        public double? V { get => _V; set
            { 
                _V = value;
                OnPropertyChanged();
                if (_Channel == SMUChannelType.A)
                {
                    AV = value;
                }
                else if (_Channel == SMUChannelType.B)
                {
                    BV = value; 
                }
            }
        }
        private double? _V;
        public double? I { get => _I; set
            { 
                _I = value; 
                OnPropertyChanged();
                if (_Channel == SMUChannelType.A)
                {
                    AI = value;
                }
                else if (_Channel == SMUChannelType.B)
                {
                    BI = value;
                }
            } 
        }
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
            this.SetIconResource("SMUDrawingImage");

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

        [CommandDisplay("EditDisplayConfig", Order = -1)]
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
