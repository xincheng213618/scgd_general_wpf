using ColorVision.MySql.DAO;
using ColorVision.Services.Device.SMU.Configs;
using ColorVision.Services.Device.SMU.Views;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Services.Device.SMU
{
    public class DeviceSMU : DeviceService<ConfigSMU>
    {
        public SMUService Service { get; set; }

        public ViewSMU View { get; set; }


        public DeviceSMU(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            Service = new SMUService(Config);
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
        public override UserControl GetDisplayControl() => new SMUDisplayControl(this);


    }
}
