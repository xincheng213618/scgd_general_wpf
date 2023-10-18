using ColorVision.Device.FileServer;
using ColorVision.Device.POI;
using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using ColorVision.Services.Algorithm;
using System.Windows.Controls;

namespace ColorVision.Device.Algorithm
{
    public class DeviceAlgorithm : BaseDevice<AlgorithmConfig>
    {
        public AlgorithmService Service { get; set; }

        public AlgorithmDisplayControl Control { get; set; }

        public AlgorithmView View { get; set; }


        public DeviceAlgorithm(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            Config = new AlgorithmConfig() { SendTopic = "Algorithm/CMD/01", SubscribeTopic = "Algorithm/STATUS/01" };
            View ??= new AlgorithmView();
            Service = new AlgorithmService(Config);
            Control = new AlgorithmDisplayControl(this);
        }

        public override UserControl GetDeviceControl() => new DeviceAlgorithmConfigControl(this);
        public override UserControl GetDisplayControl() => Control;
    }
}
