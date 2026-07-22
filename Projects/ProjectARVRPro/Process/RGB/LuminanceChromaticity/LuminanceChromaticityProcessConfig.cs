#pragma warning disable CA1707
using System.ComponentModel;

namespace ProjectARVRPro.Process.RGB.LuminanceChromaticity
{
    public class LuminanceChromaticityProcessConfig : ProcessConfigBase
    {
        [Category("输出配置")]
        [DisplayName("输出Key")]
        [Description("写入亮色度测试结果字典的Key，例如White、Red、Green、Blue")]
        public string Key { get => _Key; set { _Key = value; OnPropertyChanged(); } }
        private string _Key = "White";

        [Category("解析配置")]
        [DisplayName("Center解析Key")]
        [Description("用于解析Center数据的POI名称")]
        public string CenterKey { get => _CenterKey; set { _CenterKey = value; OnPropertyChanged(); } }
        private string _CenterKey = "P_5";

        [Browsable(false)]
        public LuminanceChromaticityRecipeConfig RecipeConfig { get => _RecipeConfig; set { _RecipeConfig = value ?? new(); OnPropertyChanged(); } }
        private LuminanceChromaticityRecipeConfig _RecipeConfig = new();

        public string GetOutputKey() => KeyedTestResultDictionary.NormalizeKey(Key, "White");
    }
}
