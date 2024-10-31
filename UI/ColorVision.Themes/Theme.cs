using System.ComponentModel;

namespace ColorVision.Themes
{
    public enum Theme
    {
        [Description("ThemeUseSystem")]
        UseSystem,
        [Description("ThemeLight")]
        Light,
        [Description("ThemeDark")]
        Dark,
        [Description("ThemePink")]
        Pink,
        [Description("ThemeCyan")] // 添加青色主题
        Cyan
    };
}
