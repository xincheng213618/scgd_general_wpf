using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Device.SMU
{
    public class DeviceSMU : BaseDevice<SMUConfig>
    {
        public SMUService Service { get; set; }

        public SMUView View { get; set; }


        public DeviceSMU(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            Service = new SMUService(Config);
            View = new SMUView();

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
        public override UserControl GetDisplayControl() => new SMUDisplayControl(this);


    }
}
