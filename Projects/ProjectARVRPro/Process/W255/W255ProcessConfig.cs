#pragma warning disable CA1707
using System.ComponentModel;
using ProjectARVRPro.Process.Uniformity;

namespace ProjectARVRPro.Process.W255
{
    public class W255ProcessConfig : ProcessConfigBase
    {
        [Category("解析配置")]
        [DisplayName("Center解析Key")]
        [Description("用于解析Center数据的Key")]
        public string Key_Center { get => _Key_Center; set { _Key_Center = value; OnPropertyChanged(); } }
        private string _Key_Center = "P_5";

        [Category("解析配置")]
        [DisplayName("从修正后POI重算均匀性")]
        [Description("关闭时按配置的TName读取模板均匀性结果；开启时使用应用Recipe修正后的POI重新计算亮度和色度均匀性。")]
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
