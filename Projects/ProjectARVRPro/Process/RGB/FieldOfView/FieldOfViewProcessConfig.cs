using System.ComponentModel;

namespace ProjectARVRPro.Process.RGB.FieldOfView
{
    public class FieldOfViewProcessConfig : ProcessConfigBase
    {
        [Category("输出配置")]
        [DisplayName("输出Key")]
        [Description("写入视场角测试结果字典的Key；White同时写入W51TestResult兼容字段")]
        public string Key { get => _Key; set { _Key = value; OnPropertyChanged(); } }
        private string _Key = "White";

        [Browsable(false)]
        public FieldOfViewRecipeConfig RecipeConfig { get => _RecipeConfig; set { _RecipeConfig = value ?? new(); OnPropertyChanged(); } }
        private FieldOfViewRecipeConfig _RecipeConfig = new();

        public string GetOutputKey() => KeyedTestResultDictionary.NormalizeKey(Key, "White");
    }
}
