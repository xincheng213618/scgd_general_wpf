using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.FileServer;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.FlowDevice
{

    public class DeviceFlowDevice : DeviceService<ConfigFlowDevice>
    {
        public MQTTDeviceService<ConfigFlowDevice> DService { get; set; }
        public IDisplayConfigBase DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<IDisplayConfigBase>(Config.Code);

        public DeviceFlowDevice(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTDeviceService<ConfigFlowDevice>(Config);
            EditCommand = new RelayCommand(a =>
            {
                var propertyEditorWindow = new PropertyEditorWindow(Config, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                propertyEditorWindow.Submited += (s, e) => Save();
                propertyEditorWindow.ShowDialog();
            }, a => AccessControl.Check(PermissionMode.Administrator));
        }

        public override UserControl GetDeviceInfo() => new UserControl();

        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
