using System.ComponentModel;

namespace ColorVision.Themes;

/// <summary>The startup window's appearance, independent of the application theme.</summary>
public enum StartupTheme
{
    [Description("StartupThemeDark")]
    Dark = 0,
    [Description("StartupThemeLight")]
    Light = 1,
    [Description("StartupThemeFollowApplication")]
    FollowApplication = 2
}
