using ColorVision.Util.Interfaces;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Services.Devices.CfwPort
{
    public class DeviceCfwPort : DeviceService<ConfigCfwPort>
    {
        public MQTTCfwPort DeviceService { get; set; }

        public DeviceCfwPort(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new MQTTCfwPort(Config);

            this.SetIconResource("CfwPortDrawingImage");
           
        }

        public override UserControl GetDeviceControl() => new InfoCfwPort(this);
        public override UserControl GetDeviceInfo() => new InfoCfwPort(this);

        public override UserControl GetDisplayControl() => new DisplayCfwPortControl(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DeviceService;
        }
    }
}
