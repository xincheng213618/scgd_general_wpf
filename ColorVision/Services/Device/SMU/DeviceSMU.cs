using ColorVision.MQTT;
using ColorVision.MySql.DAO;
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


        }
        public override UserControl GetDeviceControl() => new DeviceSMUControl(this);
        public override UserControl GetDisplayControl() => new SMUDisplayControl(this);


    }
}
