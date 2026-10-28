using ColorVision.Common.MVVM;
using ColorVision.UI.Extension;
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

            this.SetIconResource("CfwPortDrawingImage");

            EditCommand = new RelayCommand(a =>
            {
                if (!CanConfigure) return;
                var draft = CfwPortConfigurationDraft.From(Config, DisplayConfig);
                var propertyEditorWindow = new PropertyEditorWindow(draft, PropertyEditorEditMode.Transactional) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                ProtectConfigurationEditor(propertyEditorWindow);
                propertyEditorWindow.Submitted += (s, e) => TryCommitConfiguration(propertyEditorWindow,
                    () => { draft.Apply(Config, DisplayConfig); ConfigService.Instance.Save<DisplayConfigManager>(); });
                propertyEditorWindow.ShowDialog();

            }, a => CanConfigure);
        }

        private void DisplayConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DisplayCfwPortConfig.PortNum) && DisplayConfig.PortNum.HasValue)
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
