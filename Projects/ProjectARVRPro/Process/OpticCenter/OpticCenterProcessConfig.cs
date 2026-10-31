using ProjectARVRPro.Process.KeyedResults;
using System.ComponentModel;

namespace ProjectARVRPro.Process.OpticCenter
{
    public class OpticCenterProcessConfig : ProcessConfigBase<OpticCenterRecipeConfig>
    {
        public const string DefaultKey = "Optical_Center";

        [Category("输出配置")]
        [DisplayName("输出Key")]
        [Description("光学中心结果的分组标识，用于CSV的Test_Screen及批次指标名称；留空时沿用Optical_Center")]
        public string Key { get => _Key; set { _Key = value; OnPropertyChanged(); } }
        private string _Key = DefaultKey;

        public string GetOutputKey() => KeyedTestResultDictionary.NormalizeKey(Key, DefaultKey);
    }
}
