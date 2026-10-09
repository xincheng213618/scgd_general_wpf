using ColorVision.Common.MVVM;
using ColorVision.UI.Extension;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Views;
using ColorVision.Engine.Services.Devices.SMU.Local;
using ColorVision.Engine.Templates;
using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.SMU
{
    public class SMUSourceDisplayConfig : ViewModelBase
    {
        public double MeasureVal { get => _measureVal; set => SetProperty(ref _measureVal, value); }
        private double _measureVal = 5;

        public double LmtVal { get => _lmtVal; set => SetProperty(ref _lmtVal, value); }
        private double _lmtVal = 5;
    }

    public class SMUChannelDisplayConfig : ViewModelBase
    {
        public SMUSourceDisplayConfig VoltageSource { get => _voltageSource; set { _voltageSource = value ?? new SMUSourceDisplayConfig(); OnPropertyChanged(); } }
        private SMUSourceDisplayConfig _voltageSource = new();

        public SMUSourceDisplayConfig CurrentSource { get => _currentSource; set { _currentSource = value ?? new SMUSourceDisplayConfig(); OnPropertyChanged(); } }
        private SMUSourceDisplayConfig _currentSource = new();

        public double? V { get => _v; set => SetProperty(ref _v, value); }
        private double? _v;

        public double? I { get => _i; set => SetProperty(ref _i, value); }
        private double? _i;

        public SMUSourceDisplayConfig GetSourceConfig(bool isSourceV)
        {
            return isSourceV ? VoltageSource : CurrentSource;
        }
    }

    public class DisplaySMUConfig : IDisplayConfigBase
    {
        [Category("AcquisitionDisplay"), DisplayName("使用本地源表"), Description("由当前电脑通过串口或网口直接连接；在源表配置窗口编辑。关闭当前连接后切换生效。")]
        public bool UseLocalSmu { get => _useLocalSmu; set => SetProperty(ref _useLocalSmu, value); }
        private bool _useLocalSmu;

        [Category("AcquisitionDisplay"), DisplayName("本地串口波特率"), PropertyVisibility(nameof(UseLocalSmu))]
        [Description("仅本地串口连接生效，须与仪器设置一致；修改后关闭连接再重新打开。Keithley 2600/2604 支持到 115200，2400 支持到 57600。")]
        public SMUSerialBaudRate LocalBaudRate { get => _localBaudRate; set => SetProperty(ref _localBaudRate, value); }
        private SMUSerialBaudRate _localBaudRate = SMUSerialBaudRate.Baud9600;

        [DisplayName("启用限值检查")]
        public bool IsUseLimitSigned { get => _IsUseLimitSigned; set { _IsUseLimitSigned = value; OnPropertyChanged(); } }
        private bool _IsUseLimitSigned = true;

        [DisplayName("电压源模式")]
        public bool IsSourceV { get => _IsSourceV; set { _IsSourceV = value; OnPropertyChanged(); NotifySelectedSourceChanged(); } }
        private bool _IsSourceV = true;

        [DisplayName("手动测量通道")]
        public SMUChannelType Channel { get => _Channel; set { _Channel = value; OnPropertyChanged(); NotifySelectedChannelChanged(); } }
        private SMUChannelType _Channel = SMUChannelType.A;

        [Browsable(false)]
        public SMUChannelDisplayConfig ChannelA { get => _channelA; set { _channelA = value ?? new SMUChannelDisplayConfig(); OnPropertyChanged(); NotifySelectedChannelChanged(); } }
        private SMUChannelDisplayConfig _channelA = new();

        [Browsable(false)]
        public SMUChannelDisplayConfig ChannelB { get => _channelB; set { _channelB = value ?? new SMUChannelDisplayConfig(); OnPropertyChanged(); NotifySelectedChannelChanged(); } }
        private SMUChannelDisplayConfig _channelB = new();

        [JsonIgnore, Browsable(false)]
        public SMUChannelDisplayConfig CurrentChannelConfig => Channel == SMUChannelType.A ? ChannelA : ChannelB;

        [JsonIgnore, Browsable(false)]
        public SMUSourceDisplayConfig CurrentSourceConfig => CurrentChannelConfig.GetSourceConfig(IsSourceV);

        [JsonIgnore, Browsable(false)]
        public double? V { get => CurrentChannelConfig.V; set { CurrentChannelConfig.V = value; OnPropertyChanged(); } }

        [JsonIgnore, Browsable(false)]
        public double? I { get => CurrentChannelConfig.I; set { CurrentChannelConfig.I = value; OnPropertyChanged(); } }

        private void NotifySelectedChannelChanged()
        {
            OnPropertyChanged(nameof(CurrentChannelConfig));
            OnPropertyChanged(nameof(V));
            OnPropertyChanged(nameof(I));
            NotifySelectedSourceChanged();
        }

        private void NotifySelectedSourceChanged()
        {
            OnPropertyChanged(nameof(CurrentSourceConfig));
        }
    }

    public partial class DeviceSMU : DeviceService<ConfigSMU>
    {
        public MQTTSMU DService { get; set; }

        private readonly Lazy<ViewSMU> _view;
        public ViewSMU View => _view.Value;
        public DisplaySMUConfig DisplayConfig { get; }

        public DeviceSMU(SysResourceModel sysResourceModel) : this(sysResourceModel, new LocalSmuSession()) { }

        internal DeviceSMU(SysResourceModel sysResourceModel, LocalSmuSession session) : base(sysResourceModel)
        {
            LocalSession = session;
            DisplayConfig = DisplayConfigManager.Instance.GetDisplayConfig<DisplaySMUConfig>(Config.Code);
            _view = new Lazy<ViewSMU>(() => Application.Current.Dispatcher.CheckAccess()
                ? new ViewSMU()
                : Application.Current.Dispatcher.Invoke(() => new ViewSMU()));
            InitializeLocalSmu();
            DService = new MQTTSMU(this);
            DService.RefreshBackendStatus();
            this.SetIconResource("SMUDrawingImage");

            EditCommand = new RelayCommand(a =>
            {
                var window = new EditSMU(Config, DisplayConfig) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                if (window.ShowDialog() == true)
                {
                    // Local preferences remain usable even if saving the service configuration fails.
                    ConfigHandler.GetInstance().Save<DisplayConfigManager>();
                    Save();
                }
            });

            EditSMUTemplateCommand = new RelayCommand(a => EditSMUTemplate());
        }


        [CommandDisplay("SmuTemplateSettings",Order =100, CategoryOrder = 2)]
        [Category("AcquisitionDisplay")]
        [Description("CommandSmuTemplateHint")]
        public RelayCommand EditSMUTemplateCommand { get; set; }

        public static void EditSMUTemplate()
        {

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
