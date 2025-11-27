using ColorVision.Common.MVVM;
using ColorVision.ImageEditor;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.FileServer
{
    public class DeviceFileServer : DeviceService<ConfigFileServer>
    {
        public MQTTFileServer DService { get; set; }

        public ImageView View { get; set; }
        public IDisPlayConfigBase DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<IDisPlayConfigBase>(Config.Code);


        public DeviceFileServer(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTFileServer(Config);
            View = new ImageView();

            EditCommand = new RelayCommand(a =>
            {
                var propertyEditorWindow = new PropertyEditorWindow(Config, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                propertyEditorWindow.Submited += (s, e) => Save();
                propertyEditorWindow.ShowDialog();
            },a => AccessControl.Check(PermissionMode.Administrator));
        }

        public override UserControl GetDeviceInfo() => new InfoFileServer(this);

        public override UserControl GetDisplayControl() =>new FileServerDisplayControl(this);


        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
