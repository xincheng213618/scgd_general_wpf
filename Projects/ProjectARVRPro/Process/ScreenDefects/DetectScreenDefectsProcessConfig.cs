using System.ComponentModel;

namespace ProjectARVRPro.Process.ScreenDefects
{
    public sealed class DetectScreenDefectsProcessConfig : ProcessConfigBase
    {
        [Category("显示配置")]
        [DisplayName("显示格式")]
        [Description("屏幕缺陷数值的显示格式")]
        public string ShowConfig { get => _ShowConfig; set { _ShowConfig = value; OnPropertyChanged(); } }
        private string _ShowConfig = "F4";

        [Category("解析配置")]
        [DisplayName("算法名称过滤")]
        [Description("为空时解析当前批次最新的屏幕缺陷检测结果；填写后仅解析 TName 包含该文本的结果")]
        public string TemplateName { get => _TemplateName; set { _TemplateName = value; OnPropertyChanged(); } }
        private string _TemplateName = string.Empty;

        [Category("导出配置")]
        [DisplayName("结果名称")]
        [Description("ObjectiveTestResult 中显示的测试画面名称")]
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name = "ScreenDefects";
    }
}
