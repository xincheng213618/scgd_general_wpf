using ColorVision.Common.MVVM;
using ColorVision.UI.Extension;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.CfwPort
{
    public class DeviceCfwPort : DeviceService<ConfigCfwPort>
    {
        public MQTTCfwPort DService { get; set; }
        public IDisplayConfigBase DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<IDisplayConfigBase>(Config.Code);

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
            DService = new MQTTCfwPort(Config);

            this.SetIconResource("CfwPortDrawingImage");

            EditCommand = new RelayCommand(a =>
            {
                var propertyEditorWindow = new PropertyEditorWindow(Config, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                propertyEditorWindow.Submited += (s, e) => Save();
                propertyEditorWindow.ShowDialog();

            }, a => AccessControl.Check(PermissionMode.Administrator));
        }

        public override UserControl GetDeviceInfo() => new InfoCfwPort(this);

        public override UserControl GetDisplayControl() => new DisplayCfwPort(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
