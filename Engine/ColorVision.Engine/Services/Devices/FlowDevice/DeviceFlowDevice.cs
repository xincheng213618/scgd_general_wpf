using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.FlowDevice
{

    public class DeviceFlowDevice : DeviceService<ConfigFlowDevice>
    {
        public MQTTFlowDevice DService { get; set; }
        public IDisplayConfigBase DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<IDisplayConfigBase>(Config.Code);

        public DeviceFlowDevice(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTFlowDevice(Config);
            EditCommand = new RelayCommand(a =>
            {
                var propertyEditorWindow = new PropertyEditorWindow(Config, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                propertyEditorWindow.Submited += (s, e) => Save();
                propertyEditorWindow.ShowDialog();
            }, a => AccessControl.Check(PermissionMode.Administrator));
        }

        public override UserControl GetDeviceInfo() => new InfoFlowDevice(this);


        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
