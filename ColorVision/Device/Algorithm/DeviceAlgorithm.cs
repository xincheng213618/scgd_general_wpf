using ColorVision.Device.POI;
using ColorVision.MQTT;
using ColorVision.MySql.DAO;
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
            Config = new AlgorithmConfig() { SendTopic = "Algorithm", SubscribeTopic = "AlgorithmService" };
            View ??= new AlgorithmView();
            Service = new AlgorithmService(Config);
            Control = new AlgorithmDisplayControl(this);
        }


        public override UserControl GetDisplayControl() => Control;
    }
}
