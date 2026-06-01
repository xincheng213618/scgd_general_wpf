using System.ComponentModel;

namespace ProjectARVRPro.Process.Distortion
{
    public class DistortionDynamicProcessConfig : ProcessConfigBase
    {
        [Category("显示配置")]
        [DisplayName("显示格式")]
        [Description("畸变结果显示格式")]
        public string ShowConfig { get => _ShowConfig; set { _ShowConfig = value; OnPropertyChanged(); } }
        private string _ShowConfig = "F5";

        [Category("显示配置")]
        [DisplayName("9点来源")]
        [Description("结果图绘制使用的9点来源")]
        public DistortionPointSource PointSource { get => _PointSource; set { _PointSource = value; OnPropertyChanged(); } }
        private DistortionPointSource _PointSource = DistortionPointSource.TV;

        [Category("导出配置")]
        [DisplayName("导出名称")]
        [Description("导出CSV和DynamicTestResults时显示的测试画面名称")]
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name = "Distortion";

        [Category("导出配置")]
        [DisplayName("单位")]
        [Description("畸变结果单位")]
        public string Unit { get => _Unit; set { _Unit = value; OnPropertyChanged(); } }
        private string _Unit = "%";
    }
}
