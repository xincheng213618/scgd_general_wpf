using ColorVision.Media;
using ColorVision.Services;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Calibration.Views;
using ColorVision.Services.Extension;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.FileServer
{
    public class DeviceFileServer : DeviceService<FileServerConfig>
    {
        public MQTTFileServer MQTTFileServer { get; set; }

        public ImageView View { get; set; }

        public DeviceFileServer(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            MQTTFileServer = new MQTTFileServer(Config);
            View = new ImageView();
            View.View.Title = $"文件服务 - {Config.Code}";
        }

        public override UserControl GetDeviceControl() => new DeviceFileServerControl(this);
        public override UserControl GetDeviceInfo() => new DeviceFileServerControl(this,false);

        public override UserControl GetDisplayControl() =>new FileServerDisplayControl(this);
        public override MQTTServiceBase? GetMQTTService()
        {
            return MQTTFileServer;
        }
    }
}
