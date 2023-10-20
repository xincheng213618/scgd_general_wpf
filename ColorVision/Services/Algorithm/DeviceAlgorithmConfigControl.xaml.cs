using System.Windows.Controls;

namespace ColorVision.Services.Algorithm
{
    /// <summary>
    /// DeviceAlgorithmConfigControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceAlgorithmConfigControl : UserControl
    {
        public DeviceAlgorithm Device { get; set; }
        public DeviceAlgorithmConfigControl(DeviceAlgorithm device)
        {
            InitializeComponent();
            this.Device = device;
        }
    }
}
