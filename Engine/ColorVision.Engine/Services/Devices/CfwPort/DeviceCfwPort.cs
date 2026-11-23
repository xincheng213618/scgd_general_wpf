using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ColorVision.Engine.Services.Devices.CfwPort.Local;

namespace ColorVision.Engine.Services.Devices.CfwPort
{
    public partial class DeviceCfwPort : DeviceService<ConfigCfwPort>
    {
        public MQTTCfwPort DService { get; set; }
        public DisplayCfwPortConfig DisplayConfig { get; }

        public FilterWheelConfig FilterWheelConfig
        {
            get 
            {
                var phycamera =  PhyCameraManager.GetInstance().PhyCameras.FirstOrDefault(a => a.Code == Config.SN);
                if (phycamera != null)
                {
                    return phycamera.Config.FilterWheelConfig;
                }
                else
                {
                    var filterWheelConfig = new FilterWheelConfig();
                    if (Config.Ports == 12)
                        for (int index = 6; index < 12; index++) filterWheelConfig.HoleMapping.Add(new HoleMap { HoleIndex = index, HoleName = $"位置 {index}" });
                    return filterWheelConfig;
                }

            } 
        }
        public DeviceCfwPort(SysResourceModel sysResourceModel) : this(sysResourceModel, new LocalCfwPortSession()) { }

        internal DeviceCfwPort(SysResourceModel sysResourceModel, LocalCfwPortSession session) : base(sysResourceModel)
        {
            LocalSession = session;
            DisplayConfig = DisplayConfigManager.Instance.GetDisplayConfig<DisplayCfwPortConfig>(Config.Code);
            DisplayConfig.PropertyChanged += DisplayConfig_PropertyChanged;
            InitializeLocalCfwPort();
            DService = new MQTTCfwPort(Config) { Device = this };
            DService.RefreshBackendStatus();

            EditCommand = new RelayCommand(a =>
            {
                if (!CanConfigure) return;
                var window = new EditCfwPort(this) { Owner = Application.Current.GetActiveWindow() };
                if (window.ShowDialog() != true || !CanConfigure || !TryAdmitConfigurationEdit(window.OpeningTarget, window.Owner)) return;
                var (serviceChanged, displayChanged) = window.Draft.Apply(Config, DisplayConfig);
                if (displayChanged) ConfigService.Instance.Save<DisplayConfigManager>();
                if (serviceChanged) TryCommitConfiguration(window.Owner, expectedTarget: window.OpeningTarget);
            }, a => CanConfigure);
        }

        private void DisplayConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(DisplayCfwPortConfig.PortNum) or nameof(DisplayCfwPortConfig.TargetPortNum))
                ConfigService.Instance.Save<DisplayConfigManager>();
        }

        public override void Dispose()
        {
            DisplayConfig.PropertyChanged -= DisplayConfig_PropertyChanged;
            DisposeLocalCfwPort();
            base.Dispose();
            System.GC.SuppressFinalize(this);
        }

        public override UserControl GetDeviceInfo() => new InfoCfwPort(this);

        public override UserControl GetDisplayControl() => new DisplayCfwPort(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
