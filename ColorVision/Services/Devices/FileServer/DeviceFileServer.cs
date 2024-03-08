using ColorVision.Media;
using ColorVision.Services;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.FileServer
{
    public class DeviceFileServer : DeviceService<FileServerConfig>
    {
        public MQTTService DeviceService { get; set; }

        public ImageView View { get; set; }

        public DeviceFileServer(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new MQTTService(Config);
            View = new ImageView();
        }

        public override UserControl GetDeviceControl() => new DeviceFileServerControl(this);
        public override UserControl GetDeviceInfo() => new DeviceFileServerControl(this,false);

        public override UserControl GetDisplayControl() =>new FileServerDisplayControl(this);
        public override MQTTServiceBase? GetMQTTService()
        {
            return DeviceService;
        }
    }
}
