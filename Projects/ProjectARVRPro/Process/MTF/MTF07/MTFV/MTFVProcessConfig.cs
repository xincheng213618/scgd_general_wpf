#pragma warning disable CA1707
using ProjectARVRPro.Process.KeyedResults;
using ProjectARVRPro.Process.MTF.MTF07;
using System.ComponentModel;

namespace ProjectARVRPro.Process.MTF.MTFV
{
    public sealed class MTFVProcessConfig : ProcessConfigBase<MTFVRecipeConfig>, IMTF07DynamicProcessConfig
    {
        [Category("输出配置")]
        [DisplayName("输出Key")]
        [Description("写入DynamicTestResults的Key；多个MTF07-V画面应配置不同Key。")]
        public string Key { get => _Key; set { _Key = value; OnPropertyChanged(); } }
        private string _Key = "MTFV";

        [Category("显示配置")]
        [DisplayName("显示格式")]
        public string ShowConfig { get => _ShowConfig; set { _ShowConfig = value; OnPropertyChanged(); } }
        private string _ShowConfig = "F3";

        [Category("输出配置")]
        [DisplayName("单位")]
        public string Unit { get => _Unit; set { _Unit = value; OnPropertyChanged(); } }
        private string _Unit = "%";

        public string GetOutputKey() => KeyedTestResultDictionary.NormalizeKey(Key, "MTFV");
    }
}
