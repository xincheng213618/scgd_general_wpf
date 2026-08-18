#pragma warning disable CA1707
using System.ComponentModel;

namespace ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity
{
    public class LuminanceChromaticityYWProcessConfig : ProcessConfigBase
    {
        [Category("输出配置")]
        [DisplayName("输出Key")]
        [Description("写入YW亮色度测试结果字典的Key。")]
        public string Key { get => _Key; set { _Key = value; OnPropertyChanged(); } }
        private string _Key = "YW";

        [Browsable(false)]
        public LuminanceChromaticityYWRecipeConfig RecipeConfig { get => _RecipeConfig; set { _RecipeConfig = value ?? new(); OnPropertyChanged(); } }
        private LuminanceChromaticityYWRecipeConfig _RecipeConfig = new();

        public string GetOutputKey() => KeyedTestResultDictionary.NormalizeKey(Key, "YW");
    }
}
