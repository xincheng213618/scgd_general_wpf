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
        public bool IsUseLimitSigned { get => _IsUseLimitSigned; set { _IsUseLimitSigned = value; OnPropertyChanged(); } }
        private bool _IsUseLimitSigned = true;

        public bool IsSourceV { get => _IsSourceV; set { _IsSourceV = value; OnPropertyChanged(); NotifySelectedSourceChanged(); } }
        private bool _IsSourceV = true;

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

    public class DeviceSMU : DeviceService<ConfigSMU>
    {
        public MQTTSMU DService { get; set; }

        private readonly Lazy<ViewSMU> _view;
        public ViewSMU View => _view.Value;
        public DisplaySMUConfig DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<DisplaySMUConfig>(Config.Code);

        public DeviceSMU(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTSMU(this);
            _view = new Lazy<ViewSMU>(() => Application.Current.Dispatcher.CheckAccess()
                ? new ViewSMU()
                : Application.Current.Dispatcher.Invoke(() => new ViewSMU()));
            this.SetIconResource("SMUDrawingImage");

            EditCommand = new RelayCommand(a =>
            {
                var propertyEditorWindow = new PropertyEditorWindow(Config, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                propertyEditorWindow.Submited += (s, e) => Save();
                propertyEditorWindow.ShowDialog();
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
