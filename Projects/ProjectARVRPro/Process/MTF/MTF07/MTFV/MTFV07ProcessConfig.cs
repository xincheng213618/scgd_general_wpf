#pragma warning disable CA1707
using ProjectARVRPro.Process.KeyedResults;
using ProjectARVRPro.Process.MTF.MTF07;
using System.ComponentModel;

namespace ProjectARVRPro.Process.MTF.MTF07.MTFV
{
    public sealed class MTFV07ProcessConfig : ProcessConfigBase<MTFV07RecipeConfig>, IMTF07DynamicProcessConfig
    {
        [Category("输出配置")]
        [DisplayName("输出Key")]
        [Description("MTFV07竖条纹结果写入MTFV07TestResults字典的Key；多个画面应配置不同Key。")]
        public string Key { get => _Key; set { _Key = value; OnPropertyChanged(); } }
        private string _Key = "MTFV07";

        [Category("解析配置")]
        [DisplayName("Center_0F_V解析Key")]
        public string Key_Center_0F { get => _Key_Center_0F; set { _Key_Center_0F = value; OnPropertyChanged(); } }
        private string _Key_Center_0F = "Center_0F_V";

        [Category("解析配置")]
        [DisplayName("LeftUp_0.7F_V解析Key")]
        public string Key_LeftUp_0_7F { get => _Key_LeftUp_0_7F; set { _Key_LeftUp_0_7F = value; OnPropertyChanged(); } }
        private string _Key_LeftUp_0_7F = "LeftUp_0.7F_V";

        [Category("解析配置")]
        [DisplayName("RightUp_0.7F_V解析Key")]
        public string Key_RightUp_0_7F { get => _Key_RightUp_0_7F; set { _Key_RightUp_0_7F = value; OnPropertyChanged(); } }
        private string _Key_RightUp_0_7F = "RightUp_0.7F_V";

        [Category("解析配置")]
        [DisplayName("LeftDown_0.7F_V解析Key")]
        public string Key_LeftDown_0_7F { get => _Key_LeftDown_0_7F; set { _Key_LeftDown_0_7F = value; OnPropertyChanged(); } }
        private string _Key_LeftDown_0_7F = "LeftDown_0.7F_V";

        [Category("解析配置")]
        [DisplayName("RightDown_0.7F_V解析Key")]
        public string Key_RightDown_0_7F { get => _Key_RightDown_0_7F; set { _Key_RightDown_0_7F = value; OnPropertyChanged(); } }
        private string _Key_RightDown_0_7F = "RightDown_0.7F_V";

        [Category("显示配置")]
        [DisplayName("显示格式")]
        public string ShowConfig { get => _ShowConfig; set { _ShowConfig = value; OnPropertyChanged(); } }
        private string _ShowConfig = "F3";

        [Category("输出配置")]
        [DisplayName("单位")]
        public string Unit { get => _Unit; set { _Unit = value; OnPropertyChanged(); } }
        private string _Unit = "%";

        public string GetOutputKey() => KeyedTestResultDictionary.NormalizeKey(Key, "MTFV07");

        public bool TryGetItemName(string? sourceName, out string itemName)
        {
            if (Matches(sourceName, Key_Center_0F)) itemName = nameof(MTFV07TestResult.MTF_V_Center_0F);
            else if (Matches(sourceName, Key_LeftUp_0_7F)) itemName = nameof(MTFV07TestResult.MTF_V_LeftUp_0_7F);
            else if (Matches(sourceName, Key_RightUp_0_7F)) itemName = nameof(MTFV07TestResult.MTF_V_RightUp_0_7F);
            else if (Matches(sourceName, Key_LeftDown_0_7F)) itemName = nameof(MTFV07TestResult.MTF_V_LeftDown_0_7F);
            else if (Matches(sourceName, Key_RightDown_0_7F)) itemName = nameof(MTFV07TestResult.MTF_V_RightDown_0_7F);
            else
            {
                itemName = string.Empty;
                return false;
            }

            return true;
        }

        private static bool Matches(string? sourceName, string? configuredKey) =>
            !string.IsNullOrWhiteSpace(sourceName) &&
            !string.IsNullOrWhiteSpace(configuredKey) &&
            string.Equals(sourceName.Trim(), configuredKey.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
