using System.ComponentModel;

namespace ProjectARVRPro.Process.OpticCenter
{
    public class OpticCenterDynamicProcessConfig : ProcessConfigBase
    {
        [Category("显示配置")]
        [DisplayName("显示格式")]
        [Description("光轴校准结果显示格式")]
        public string ShowConfig { get => _ShowConfig; set { _ShowConfig = value; OnPropertyChanged(); } }
        private string _ShowConfig = "F4";

        [Category("导出配置")]
        [DisplayName("导出名称")]
        [Description("导出CSV和DynamicTestResults时显示的测试画面名称")]
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name = "Optical_Center";

        [Category("导出配置")]
        [DisplayName("单位")]
        [Description("光轴校准结果单位")]
        public string Unit { get => _Unit; set { _Unit = value; OnPropertyChanged(); } }
        private string _Unit = "degree";
    }
}
