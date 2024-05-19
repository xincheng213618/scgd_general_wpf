using ColorVision.UI.Configs;
using ColorVision.UI.Properties;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.UI.Languages
{

    public class LaunagesConfigSeting : IConfigSetting
    {
        public string Name => Resources.Language;

        public string Description => Resources.Language;

        public string BindingName => "Theme";

        public object Source => LanguageConfig.Instance;

        public ConfigSettingType Type => ConfigSettingType.ComboBox;
        public UserControl UserControl => throw new System.NotImplementedException();

        public ComboBox ComboBox
        {
            get
            {
                ComboBox cmlauage = new ComboBox() { SelectedValuePath = "Key", DisplayMemberPath = "Value" };
                Binding themeBinding = new Binding("UICulture");
                cmlauage.SetBinding(ComboBox.SelectedValueProperty, themeBinding);
                cmlauage.ItemsSource = from e1 in LanguageManager.Current.Languages
                                       select new KeyValuePair<string, string>(e1, LanguageManager.keyValuePairs.TryGetValue(e1, out string value) ? value : e1);
                string temp = Thread.CurrentThread.CurrentUICulture.Name;
                cmlauage.SelectionChanged += (s, e) =>
                {
                    if (cmlauage.SelectedValue is string str)
                    {
                        if (!LanguageManager.Current.LanguageChange(str))
                        {
                            LanguageConfig.Instance.UICulture = temp;
                        }
                    }
                };
                cmlauage.DataContext = LanguageConfig.Instance;
                return cmlauage;
            }
        }
    }

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
