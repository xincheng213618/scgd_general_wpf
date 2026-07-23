using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;

namespace ColorVision.Themes
{
    public class ThemeConfig : ViewModelBase, IConfig
    {
        public static ThemeConfig Instance => ConfigService.Instance.GetRequiredService<ThemeConfig>();

        /// <summary>
        /// 主题
        /// </summary>
        [ConfigSetting(Order = -40, Section = ConfigSettingConstants.SectionBasic, Description = "选择应用界面的颜色主题。", Layout = ConfigSettingLayout.Wide)]
        [Description("选择应用界面的颜色主题。")]
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
            }
        }
        private Theme _Theme = Theme.UseSystem;

        public bool TransparentWindow { get => _TransparentWindow; set { _TransparentWindow = value; OnPropertyChanged(); } }
        private bool _TransparentWindow = true;
    }
}
