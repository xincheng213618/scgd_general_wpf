using ColorVision.MySql.DAO;
using ColorVision.Services.Device;
using System.Windows.Controls;

namespace ColorVision.Device.FileServer
{
    public class DeviceFileServer : BaseDevice<FileServerConfig>
    {
        public FileServerService Service { get; set; }

        public ImageView View { get; set; }

        public DeviceFileServer(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            Service = new FileServerService(Config);
            View = new ImageView();
        }

        public override UserControl GetDeviceControl() => new DeviceFileServerControl(this);
        public override UserControl GetDeviceInfo() => new DeviceFileServerControl(this,false);

        public override UserControl GetDisplayControl() =>new FileServerDisplayControl(this);

    }
}
