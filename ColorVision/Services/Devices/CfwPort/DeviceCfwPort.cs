using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Algorithm.Views;
using ColorVision.Services.Extension;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Services.Devices.CfwPort
{
    public class DeviceCfwPort : DeviceService<ConfigCfwPort>
    {
        public MQTTCfwPort DeviceService { get; set; }

        public DeviceCfwPort(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new MQTTCfwPort(Config);

            this.SetIconResource("CfwPortDrawingImage");
           
        }

        public override UserControl GetDeviceControl() => new DeviceCfwPortControl(this);
        public override UserControl GetDeviceInfo() => new DeviceCfwPortControl(this, false);

        public override UserControl GetDisplayControl() => new DisplayCfwPortControl(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DeviceService;
        }
    }
}
