#pragma warning disable CA1707
using ColorVision.Engine.Media;
using System.ComponentModel;
using ProjectARVRPro.Process.Uniformity;

namespace ProjectARVRPro.Process.W255
{
    public class W255ProcessConfig : ProcessConfigBase<W255RecipeConfig>
    {
        [Category("解析配置")]
        [DisplayName("Center解析Key")]
        [Description("用于解析Center数据的Key")]
        public string Key_Center { get => _Key_Center; set { _Key_Center = value; OnPropertyChanged(); } }
        private string _Key_Center = "P_5";

        [Category("显示配置")]
        [DisplayName("显示内容")]
        [Description("选择关注点图层显示的字段和小数位；仅影响结果图绘制。")]
        [PropertyEditorType(typeof(CvcieTemplatePropertiesEditor))]
        public string DisplayTemplate { get => _DisplayTemplate; set { _DisplayTemplate = value ?? string.Empty; OnPropertyChanged(); } }
        private string _DisplayTemplate = PoiDisplayTemplateDefaults.Cie;

        [Category("显示配置")]
        [DisplayName("亮度均匀性格式")]
        [Description("选择百分比或小数显示，并同步结果单位；不改变内部比例数值和Recipe限值。")]
        public UniformityDisplayFormat LuminanceUniformityDisplayFormat { get => _luminanceUniformityDisplayFormat; set { _luminanceUniformityDisplayFormat = value; OnPropertyChanged(); } }
        private UniformityDisplayFormat _luminanceUniformityDisplayFormat = UniformityDisplayFormat.Percentage;

        [Category("显示配置")]
        [DisplayName("色度均匀性格式")]
        [Description("选择百分比或小数显示，并同步结果单位；不改变内部比例数值和Recipe限值。")]
        public UniformityDisplayFormat ColorUniformityDisplayFormat { get => _colorUniformityDisplayFormat; set { _colorUniformityDisplayFormat = value; OnPropertyChanged(); } }
        private UniformityDisplayFormat _colorUniformityDisplayFormat = UniformityDisplayFormat.Decimal;

        [Category("解析配置")]
        [DisplayName("从修正后POI重算均匀性")]
        [Description("关闭时优先按配置的TName读取模板均匀性结果，缺失项从原始POI补算；开启时使用应用Recipe修正后的POI重新计算亮度和色度均匀性。")]
        public bool CalculateUniformityFromCorrectedPoi { get => _CalculateUniformityFromCorrectedPoi; set { _CalculateUniformityFromCorrectedPoi = value; OnPropertyChanged(); } }
        private bool _CalculateUniformityFromCorrectedPoi;

        [Category("解析配置")]
        [DisplayName("亮度均匀性结果名称")]
        [Description("模板结果模式下用于匹配TName的文本，默认Luminance_uniformity。")]
        public string LuminanceUniformityResultName { get => _LuminanceUniformityResultName; set { _LuminanceUniformityResultName = value; OnPropertyChanged(); } }
        private string _LuminanceUniformityResultName = LuminanceChromaticityUniformityCalculator.DefaultLuminanceResultName;

        [Category("解析配置")]
        [DisplayName("色度均匀性结果名称")]
        [Description("模板结果模式下用于匹配TName的文本，默认Color_uniformity。")]
        public string ColorUniformityResultName { get => _ColorUniformityResultName; set { _ColorUniformityResultName = value; OnPropertyChanged(); } }
        private string _ColorUniformityResultName = LuminanceChromaticityUniformityCalculator.DefaultColorResultName;

        public string GetLuminanceUniformityResultName() => LuminanceChromaticityUniformityCalculator.NormalizeResultName(LuminanceUniformityResultName, LuminanceChromaticityUniformityCalculator.DefaultLuminanceResultName);

        public string GetColorUniformityResultName() => LuminanceChromaticityUniformityCalculator.NormalizeResultName(ColorUniformityResultName, LuminanceChromaticityUniformityCalculator.DefaultColorResultName);

    }
}
