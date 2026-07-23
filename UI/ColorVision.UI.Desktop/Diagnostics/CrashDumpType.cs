using System.ComponentModel;

namespace ColorVision.UI.Desktop.Diagnostics
{
    public enum CrashDumpType
    {
        [Description("自定义转储")]
        Custom = 0,

        [Description("小型转储（推荐）")]
        Mini = 1,

        [Description("完整内存转储")]
        Full = 2
    }
}
