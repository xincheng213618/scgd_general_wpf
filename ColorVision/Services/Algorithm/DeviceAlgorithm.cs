using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using System.Windows.Controls;

namespace ColorVision.Services.Algorithm
{
    public class DeviceAlgorithm : BaseDevice<AlgorithmConfig>
    {
        public AlgorithmService Service { get; set; }

        public AlgorithmDisplayControl Control { get; set; }

        public AlgorithmView View { get; set; }


        public DeviceAlgorithm(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            View ??= new AlgorithmView();
            Config.Endpoint = "tcp://192.168.1.7:6550";
            Service = new AlgorithmService(Config);
            Control = new AlgorithmDisplayControl(this);
        }

        public override UserControl GetDeviceControl() => new DeviceAlgorithmConfigControl(this);
        public override UserControl GetDisplayControl() => Control;
    }
}
