using ColorVision.Common.MVVM;
using ColorVision.Media;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Utilities;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.FileServer
{
    public class DeviceFileServer : DeviceService<ConfigFileServer>
    {
        public MQTTFileServer MQTTFileServer { get; set; }

        public ImageView View { get; set; }

        public DeviceFileServer(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            MQTTFileServer = new MQTTFileServer(Config);
            View = new ImageView();
            View.View.Title = $"文件服务 - {Config.Code}";

            EditCommand = new RelayCommand(a =>
            {
                EditFileServer window = new EditFileServer(this);
                window.Owner = WindowHelpers.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            });
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
