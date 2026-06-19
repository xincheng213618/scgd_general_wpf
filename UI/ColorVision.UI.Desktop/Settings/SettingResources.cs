using ColorVision.UI.Properties;

namespace ColorVision.UI.Desktop.Settings
{
    internal static class SettingResources
    {
        public static string NoMatchingSettings => GetString("SettingsNoMatchingSettings");
        public static string EditorUnavailable => GetString("SettingsEditorUnavailable");
        public static string EditorLoadFailed => GetString("SettingsEditorLoadFailed");
        public static string PageTypeInvalid => GetString("SettingsPageTypeInvalid");
        public static string PageUnavailable => GetString("SettingsPageUnavailable");
        public static string PageLoadFailed => GetString("SettingsPageLoadFailed");
        public static string StartupCheckUpdates => GetString("SettingsStartupCheckUpdates");
        public static string StartupCheckUpdatesDescription => GetString("SettingsStartupCheckUpdatesDescription");
        public static string StartupCheckUpdatesSearchAliases => GetString("SettingsStartupCheckUpdatesSearchAliases");
        public static string SectionBasic => GetString("SettingsSectionBasic");
        public static string SectionSearch => GetString("SettingsSectionSearch");
        public static string SectionFileArchive => GetString("SettingsSectionFileArchive");
        public static string SectionServices => GetString("SettingsSectionServices");
        public static string SectionPaths => GetString("SettingsSectionPaths");
        public static string SectionExtensions => GetString("SettingsSectionExtensions");
        public static string SectionOther => GetString("SettingsSectionOther");

        private static readonly IReadOnlyDictionary<string, string> DefaultTexts = new Dictionary<string, string>
        {
            ["SettingsNoMatchingSettings"] = "没有匹配的设置",
            ["SettingsEditorUnavailable"] = "该设置暂不可编辑",
            ["SettingsEditorLoadFailed"] = "设置编辑器加载失败",
            ["SettingsPageTypeInvalid"] = "设置页面类型无效",
            ["SettingsPageUnavailable"] = "设置页面不可用",
            ["SettingsPageLoadFailed"] = "设置页面加载失败",
            ["SettingsStartupCheckUpdates"] = "启动时检查更新",
            ["SettingsStartupCheckUpdatesDescription"] = "启动软件时检查程序和插件更新",
            ["SettingsStartupCheckUpdatesSearchAliases"] = "启动 检查更新 自动更新 插件更新",
            ["SettingsSectionBasic"] = "基础",
            ["SettingsSectionSearch"] = "搜索",
            ["SettingsSectionFileArchive"] = "文件归档",
            ["SettingsSectionServices"] = "高级服务",
            ["SettingsSectionPaths"] = "底层路径",
            ["SettingsSectionExtensions"] = "扩展页面",
            ["SettingsSectionOther"] = "其他"
        };

        private static string GetString(string key)
        {
            return Resources.ResourceManager.GetString(key, Thread.CurrentThread.CurrentUICulture)
                ?? DefaultTexts.GetValueOrDefault(key)
                ?? key;
        }
    }
}
