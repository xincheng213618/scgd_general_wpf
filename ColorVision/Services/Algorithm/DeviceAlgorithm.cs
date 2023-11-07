using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Services.Algorithm
{
    public class DeviceAlgorithm : BaseDevice<AlgorithmConfig>
    {
        public AlgorithmService Service { get; set; }
        public AlgorithmView View { get; set; }
        public ImageSource Icon { get; set; }


        public DeviceAlgorithm(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            View ??= new AlgorithmView();
            Service = new AlgorithmService(Config);

            if (Application.Current.TryFindResource("DrawingImageAlgorithm") is DrawingImage DrawingImageAlgorithm)
                Icon = DrawingImageAlgorithm;
        }

        public override UserControl GetDeviceControl() => new DeviceAlgorithmControl(this);
        public override UserControl GetDisplayControl() => new DisplayAlgorithmControl(this);
    }
}
