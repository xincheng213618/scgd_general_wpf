using ColorVision.MySql.DAO;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Services.Device.CfwPort
{
    public class DeviceCfwPort : BaseDevice<ConfigCfwPort>
    {
        public DeviceServiceCfwPort DeviceService { get; set; }

        public DeviceCfwPort(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new DeviceServiceCfwPort(Config);

            if (Application.Current.TryFindResource("CfwPortDrawingImage") is DrawingImage drawingImage)
                Icon = drawingImage;

            ThemeManager.Current.CurrentUIThemeChanged += (s) =>
            {
                if (Application.Current.TryFindResource("CfwPortDrawingImage") is DrawingImage drawingImage)
                    Icon = drawingImage;
            };
            
        }

        public override UserControl GetDeviceControl() => new DeviceCfwPortControl(this);
        public override UserControl GetDeviceInfo() => new DeviceCfwPortControl(this, false);

        public override UserControl GetDisplayControl() => new DisplayCfwPortControl(this);


    }
}
