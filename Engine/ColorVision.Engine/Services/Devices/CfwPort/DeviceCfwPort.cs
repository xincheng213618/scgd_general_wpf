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

namespace ColorVision.Engine.Services.Devices.CfwPort
{
    public class DeviceCfwPort : DeviceService<ConfigCfwPort>
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
                    return filterWheelConfig;
                }

            } 
        }
        public DeviceCfwPort(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DisplayConfig = DisplayConfigManager.Instance.GetDisplayConfig<DisplayCfwPortConfig>(Config.Code);
            DisplayConfig.PropertyChanged += DisplayConfig_PropertyChanged;
            DService = new MQTTCfwPort(Config);

            this.SetIconResource("CfwPortDrawingImage");

            EditCommand = new RelayCommand(a =>
            {
                var propertyEditorWindow = new PropertyEditorWindow(Config, PropertyEditorEditMode.Transactional) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                propertyEditorWindow.Submitted += (s, e) => Save();
                propertyEditorWindow.ShowDialog();

            }, a => AccessControl.Check(PermissionMode.Administrator));
        }

        private void DisplayConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DisplayCfwPortConfig.PortNum) && DisplayConfig.PortNum.HasValue)
                ConfigService.Instance.Save<DisplayConfigManager>();
        }

        public override void Dispose()
        {
            DisplayConfig.PropertyChanged -= DisplayConfig_PropertyChanged;
            base.Dispose();
        }

        public override UserControl GetDeviceInfo() => new InfoCfwPort(this);

        public override UserControl GetDisplayControl() => new DisplayCfwPort(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
