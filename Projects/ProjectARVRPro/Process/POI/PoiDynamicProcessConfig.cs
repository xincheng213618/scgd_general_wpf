using ColorVision.Engine.Media;
using System.ComponentModel;

namespace ProjectARVRPro.Process.POI
{
    public class PoiDynamicProcessConfig : ProcessConfigBase
    {
        [Category("显示配置")]
        [DisplayName("显示格式")]
        [Description("关注点结果显示格式")]
        public string ShowConfig { get => _ShowConfig; set { _ShowConfig = value; OnPropertyChanged(); } }
        private string _ShowConfig = "F4";

        [Category("显示配置")]
        [DisplayName("显示内容")]
        [Description("选择关注点图层显示的字段和小数位；仅影响结果图绘制。")]
        [PropertyEditorType(typeof(CvcieTemplatePropertiesEditor))]
        public string DisplayTemplate { get => _DisplayTemplate; set { _DisplayTemplate = value ?? string.Empty; OnPropertyChanged(); } }
        private string _DisplayTemplate = PoiDisplayTemplateDefaults.Cie;

        [Category("解析配置")]
        [DisplayName("算法名称过滤")]
        [Description("为空时解析当前批次所有 POI_XYZ；填写后仅解析 TName 包含该文本的结果")]
        public string TemplateName { get => _TemplateName; set { _TemplateName = value; OnPropertyChanged(); } }
        private string _TemplateName = string.Empty;

        [Category("解析配置")]
        [DisplayName("无关注点判失败")]
        [Description("未解析到关注点时是否将本流程结果判为失败")]
        public bool FailWhenEmpty { get => _FailWhenEmpty; set { _FailWhenEmpty = value; OnPropertyChanged(); } }
        private bool _FailWhenEmpty = true;

        [Category("导出配置")]
        [DisplayName("导出名称")]
        [Description("导出CSV和DynamicPoixyuvDatas时显示的测试画面名称")]
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name = "POI";
    }
}
