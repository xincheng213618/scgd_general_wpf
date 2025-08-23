using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using System.Windows;
using System.Windows.Controls;
using ColorVision.UI.Authorizations;
using ColorVision.ImageEditor;

namespace ColorVision.Engine.Services.Devices.FileServer
{
    public class DeviceFileServer : DeviceService<ConfigFileServer>
    {
        public MQTTFileServer DService { get; set; }

        public ImageView View { get; set; }

        public DeviceFileServer(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTFileServer(Config);
            View = new ImageView();

            EditCommand = new RelayCommand(a =>
            {
                EditFileServer window = new EditFileServer(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
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
