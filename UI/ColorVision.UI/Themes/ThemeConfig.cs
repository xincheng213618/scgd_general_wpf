using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;

namespace ColorVision.Themes
{
    public partial class ThemeConfig : ViewModelBase, IConfig
    {
        public static ThemeConfig Instance => ConfigService.Instance.GetRequiredService<ThemeConfig>();

        /// <summary>
        /// 主题
        /// </summary>
        [ConfigSetting(Order = -40, Group = "Appearance", Name = "AppearanceMode", Section = "VisualStyle", Layout = ConfigSettingLayout.InlineAuto)]
        [PropertyEditorType(typeof(ThemePropertiesEditor))]
        public Theme Theme
        {
            get => _Theme;
            set
            {
                Theme normalizedTheme = ThemeManager.NormalizeTheme(value);
                if (_Theme == normalizedTheme) return;
                _Theme = normalizedTheme;
                OnPropertyChanged();
                RefreshAppearanceProperties();
            }
        }
        private Theme _Theme = Theme.UseSystem;

        [ConfigSetting(Order = -25, Group = "Appearance", Section = "AppearanceStartup", Description = "StartupThemeDescription")]
        [DisplayName("StartupTheme"), Description("StartupThemeDescription")]
        [PropertyEditorType(typeof(EnumPropertiesEditor))]
        public StartupTheme StartupTheme
        {
            get => _StartupTheme;
            set
            {
                StartupTheme normalizedTheme = value is StartupTheme.Dark or StartupTheme.Light or StartupTheme.FollowApplication
                    ? value : StartupTheme.Dark;
                if (_StartupTheme == normalizedTheme) return;
                _StartupTheme = normalizedTheme;
                OnPropertyChanged();
            }
        }
        private StartupTheme _StartupTheme = StartupTheme.Dark;

        public bool TransparentWindow { get => _TransparentWindow; set { _TransparentWindow = value; OnPropertyChanged(); } }
        private bool _TransparentWindow = true;
    }
}
