using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.FlowDevice;
using ColorVision.ImageEditor;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.FileServer
{
    public class DeviceFileServer : DeviceService<ConfigFileServer>
    {
        public MQTTDeviceService<ConfigFileServer> DService { get; set; }

        public ImageView View { get; set; }
        public IDisplayConfigBase DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<IDisplayConfigBase>(Config.Code);


        public DeviceFileServer(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTDeviceService<ConfigFileServer>(Config);
            View = new ImageView();

            EditCommand = new RelayCommand(a =>
            {
                var propertyEditorWindow = new PropertyEditorWindow(Config, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                propertyEditorWindow.Submited += (s, e) => Save();
                propertyEditorWindow.ShowDialog();
            },a => AccessControl.Check(PermissionMode.Administrator));
        }

        public override UserControl GetDeviceInfo() => new UserControl();

        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
