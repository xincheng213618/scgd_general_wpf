using ColorVision.Device.Camera;
using ColorVision.Device.Sensor;
using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Mvvm.Contracts;

namespace ColorVision.Device.Image
{
    public class DeviceImage : BaseDevice<ImageConfig>
    {
        public ImageService Service { get; set; }

        public ImageDisplayControl Control { get; set; }

        public ImageView View { get; set; }

        public DeviceImage(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            Service = new ImageService(Config);
            View = new ImageView();
        }

        public override UserControl GetDeviceControl() => new DeviceImageControl(this);
        public override UserControl GetDisplayControl() => Control??new ImageDisplayControl(this);

    }
}
