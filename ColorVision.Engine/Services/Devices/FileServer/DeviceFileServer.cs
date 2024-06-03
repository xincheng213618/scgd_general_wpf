using ColorVision.Common.MVVM;
using ColorVision.Engine.Media;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Common.Utilities;
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
                EditFileServer window = new(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            });
        }

        public override UserControl GetDeviceControl() => new InfoFileServer(this);
        public override UserControl GetDeviceInfo() => new InfoFileServer(this,false);

        public override UserControl GetDisplayControl() =>new FileServerDisplayControl(this);


        public override MQTTServiceBase? GetMQTTService()
        {
            return MQTTFileServer;
        }
    }
}
