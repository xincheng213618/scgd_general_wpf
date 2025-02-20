using ColorVision.UI.Properties;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.UI.Languages
{
    public class LanguageConfig:IConfig, IConfigSettingProvider
    {
        public static LanguageConfig Instance => ConfigService.Instance.GetRequiredService<LanguageConfig>();

        /// <summary>
        /// 语言
        /// </summary>
        public string UICulture
        {
            get => LanguageManager.GetDefaultLanguages().Contains(_UICulture) ? _UICulture : CultureInfo.InstalledUICulture.Name;
            set { _UICulture = value; }
        }
        private string _UICulture = CultureInfo.InstalledUICulture.Name;



        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            ComboBox cmlauage = new ComboBox() { SelectedValuePath = "Key", DisplayMemberPath = "Value" };
            BindingExpressionBase bindingExpressionBase = cmlauage.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedValueProperty, new Binding(nameof(UICulture)));
            cmlauage.ItemsSource = from e1 in LanguageManager.Current.Languages
                                   select new KeyValuePair<string, string>(e1, LanguageManager.keyValuePairs.TryGetValue(e1, out string value) ? value : e1);
            string temp = Thread.CurrentThread.CurrentUICulture.Name;
            cmlauage.SelectionChanged += (s, e) =>
            {
                if (cmlauage.SelectedValue is string str)
                {
                    if (!LanguageManager.Current.LanguageChange(str))
                    {
                        Instance.UICulture = temp;
                    }
                }
            };
            cmlauage.DataContext = Instance;

            return new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    Name = Resources.Language,
                    Description = Resources.Language,
                    Type = ConfigSettingType.ComboBox,
                    BindingName = nameof(UICulture),
                    Source = Instance,
                    ComboBox = cmlauage
                }
            };
        }
    }
}
