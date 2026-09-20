#pragma warning disable CA1707
using ColorVision.Engine.Media;
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

        [Category("显示配置")]
        [DisplayName("显示内容")]
        [Description("选择关注点图层显示的字段和小数位；仅影响结果图绘制。")]
        [PropertyEditorType(typeof(CvcieTemplatePropertiesEditor))]
        public string DisplayTemplate { get => _DisplayTemplate; set { _DisplayTemplate = value ?? string.Empty; OnPropertyChanged(); } }
        private string _DisplayTemplate = PoiDisplayTemplateDefaults.Luminance;

        [Browsable(false)]
        public LuminanceChromaticityYWRecipeConfig RecipeConfig { get => _RecipeConfig; set { _RecipeConfig = value ?? new(); OnPropertyChanged(); } }
        private LuminanceChromaticityYWRecipeConfig _RecipeConfig = new();

        public string GetOutputKey() => KeyedTestResultDictionary.NormalizeKey(Key, "YW");
    }
}
