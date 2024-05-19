using ColorVision.UI.Configs;
using ColorVision.UI.Properties;
using ColorVision.UI.Views;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.UI.Languages
{

    public class LaunagesConfigSetingProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            ComboBox cmlauage = new ComboBox() { SelectedValuePath = "Key", DisplayMemberPath = "Value" };
            cmlauage.SetBinding(ComboBox.SelectedValueProperty, new Binding(nameof(LanguageConfig.UICulture)));
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

            return new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    Name = Resources.Language,
                    Description = Resources.Language,
                    Type = ConfigSettingType.ComboBox,
                    BindingName = nameof(LanguageConfig.UICulture),
                    Source = LanguageConfig.Instance,
                    ComboBox = cmlauage
                }
            };
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
