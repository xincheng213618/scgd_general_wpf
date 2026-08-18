#pragma warning disable CA1707
using System.ComponentModel;

namespace ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity
{
    public class LuminanceChromaticityYWProcessConfig : ProcessConfigBase
    {
        public const string DefaultPoi12X7ResultName = "POI_W255_12x7_YW";
        public const string DefaultPoi8X7ResultName = "POI_W255_8x7_YW";

        [Category("输出配置")]
        [DisplayName("输出Key")]
        [Description("写入YW亮色度测试结果字典的Key。")]
        public string Key { get => _Key; set { _Key = value; OnPropertyChanged(); } }
        private string _Key = "YW";

        [Category("解析配置")]
        [DisplayName("12X7 POI结果名称")]
        [Description("用于从数据库精确匹配12X7 POI_XYZ结果的TName，默认POI_W255_12x7_YW。")]
        public string Poi12X7ResultName { get => _Poi12X7ResultName; set { _Poi12X7ResultName = value; OnPropertyChanged(); } }
        private string _Poi12X7ResultName = DefaultPoi12X7ResultName;

        [Category("解析配置")]
        [DisplayName("8X7 POI结果名称")]
        [Description("用于从数据库精确匹配8X7 POI_XYZ结果的TName，默认POI_W255_8x7_YW。")]
        public string Poi8X7ResultName { get => _Poi8X7ResultName; set { _Poi8X7ResultName = value; OnPropertyChanged(); } }
        private string _Poi8X7ResultName = DefaultPoi8X7ResultName;

        [Browsable(false)]
        public LuminanceChromaticityYWRecipeConfig RecipeConfig { get => _RecipeConfig; set { _RecipeConfig = value ?? new(); OnPropertyChanged(); } }
        private LuminanceChromaticityYWRecipeConfig _RecipeConfig = new();

        public string GetOutputKey() => KeyedTestResultDictionary.NormalizeKey(Key, "YW");

        public string GetPoi12X7ResultName() => NormalizeResultName(Poi12X7ResultName, DefaultPoi12X7ResultName);

        public string GetPoi8X7ResultName() => NormalizeResultName(Poi8X7ResultName, DefaultPoi8X7ResultName);

        public bool IsPoi12X7Result(string? resultName) => MatchesResultName(resultName, GetPoi12X7ResultName());

        public bool IsPoi8X7Result(string? resultName) => MatchesResultName(resultName, GetPoi8X7ResultName());

        private static string NormalizeResultName(string? value, string defaultValue) => string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();

        private static bool MatchesResultName(string? actualName, string expectedName) => string.Equals(actualName?.Trim(), expectedName, StringComparison.OrdinalIgnoreCase);
    }
}
