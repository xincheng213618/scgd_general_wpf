using System.Globalization;

namespace ColorVision.UI.Languages
{
    public class LanguageConfig:IConfig
    {
        public static LanguageConfig Instance => ConfigHandler.GetInstance().GetRequiredService<LanguageConfig>();

        /// <summary>
        /// 语言
        /// </summary>
        public string UICulture
        {
            get => LanguageManager.GetDefaultLanguages().Contains(_UICulture) ? _UICulture : CultureInfo.InstalledUICulture.Name;
            set { _UICulture = value; }
        }
        private string _UICulture = CultureInfo.InstalledUICulture.Name;
    }
}
