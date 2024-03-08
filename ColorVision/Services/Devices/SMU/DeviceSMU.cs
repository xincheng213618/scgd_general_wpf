using ColorVision.Services.Dao;
using ColorVision.Services.Devices.SMU.Configs;
using ColorVision.Services.Devices.SMU.Views;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Services.Devices.SMU
{
    public class DeviceSMU : DeviceService<ConfigSMU>
    {
        public MQTTSMU Service { get; set; }

        public ViewSMU View { get; set; }


        public DeviceSMU(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            Service = new MQTTSMU(Config);
            View = new ViewSMU();

            if (Application.Current.TryFindResource("SMUDrawingImage") is DrawingImage SMUDrawingImage)
                Icon = SMUDrawingImage;

            ThemeManager.Current.CurrentUIThemeChanged += (s) =>
            {
                if (Application.Current.TryFindResource("SMUDrawingImage") is DrawingImage drawingImage)
                    Icon = drawingImage;
                View.View.Icon = Icon;
            };

            View.View.Title = "源表";
            View.View.Icon = Icon;
        }
        public override UserControl GetDeviceControl() => new DeviceSMUControl(this);
        public override UserControl GetDeviceInfo() => new DeviceSMUControl(this, false);
        public override UserControl GetDisplayControl() => new DisplaySMUControl(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return Service;
        }
    }
}
