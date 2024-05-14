using ColorVision.UI;

namespace ColorVision.Themes
{
    public class ThemeConfig:IConfig
    {
        public static ThemeConfig Instance => ConfigHandler1.GetInstance().GetRequiredService<ThemeConfig>();

        /// <summary>
        /// 主题
        /// </summary>
        public Theme Theme { get; set; } = Theme.UseSystem;
    }
}
