using ColorVision.Common.MVVM;
using ColorVision.UI;

namespace ColorVision.Themes
{
    public class ThemeConfig: ViewModelBase,IConfig
    {
        public static ThemeConfig Instance => ConfigService.Instance.GetRequiredService<ThemeConfig>();

        /// <summary>
        /// 主题
        /// </summary>
        public Theme Theme { get; set; } = Theme.UseSystem;

        public bool TransparentWindow { get => _TransparentWindow; set { _TransparentWindow = value; OnPropertyChanged(); } }
        private bool _TransparentWindow = true;
    }
}
