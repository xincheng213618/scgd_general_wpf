#pragma warning disable CA1720
using System.ComponentModel;

namespace ColorVision.Engine.Templates.SysDictionary
{
    public enum SValueType
    {
        [DescriptionAttribute("整数")]
        Integer = 0,  // 整数
        [DescriptionAttribute("浮点")]
        Float = 1,    // 浮点

        [DescriptionAttribute("布尔")]
        Boolean = 2,  // 布尔
        [DescriptionAttribute("字符串")]
        String = 3,   // 字符串
        [DescriptionAttribute("枚举")]
        Enum = 4      // 枚举
    }
}
