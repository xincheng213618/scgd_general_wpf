using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Services.Devices.Motor
{
    public class DeviceMotor : DeviceService<ConfigMotor>
    {
        public MQTTMotor DeviceService { get; set; }

        public DeviceMotor(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new MQTTMotor(Config);

            if (Application.Current.TryFindResource("COMDrawingImage") is DrawingImage drawingImage)
                Icon = drawingImage;

            ThemeManager.Current.CurrentUIThemeChanged += (s) =>
            {
                if (Application.Current.TryFindResource("COMDrawingImage") is DrawingImage drawingImage)
                    Icon = drawingImage;
            };
        }

        public override UserControl GetDeviceControl() => new InfoMotor(this);
        public override UserControl GetDeviceInfo() => new InfoMotor(this, false);

        public override UserControl GetDisplayControl() => new DisplayMotorControl(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DeviceService;
        }
    }
}
