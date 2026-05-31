using ColorVision.UI.Properties;

namespace ColorVision.UI.Desktop.Settings
{
    internal static class SettingResources
    {
        public static string NoMatchingSettings => GetString("SettingsNoMatchingSettings");
        public static string MatchingCountFormat => GetString("SettingsMatchingCountFormat");
        public static string CountFormat => GetString("SettingsCountFormat");
        public static string EditorUnavailable => GetString("SettingsEditorUnavailable");
        public static string EditorLoadFailed => GetString("SettingsEditorLoadFailed");
        public static string PageTypeInvalid => GetString("SettingsPageTypeInvalid");
        public static string PageUnavailable => GetString("SettingsPageUnavailable");
        public static string PageLoadFailed => GetString("SettingsPageLoadFailed");
        public static string SectionBasic => GetString("SettingsSectionBasic");
        public static string SectionSearch => GetString("SettingsSectionSearch");
        public static string SectionFileArchive => GetString("SettingsSectionFileArchive");
        public static string SectionServices => GetString("SettingsSectionServices");
        public static string SectionPaths => GetString("SettingsSectionPaths");
        public static string SectionExtensions => GetString("SettingsSectionExtensions");
        public static string SectionOther => GetString("SettingsSectionOther");

        private static string GetString(string key)
        {
            return Resources.ResourceManager.GetString(key, Thread.CurrentThread.CurrentUICulture) ?? key;
        }
    }
}