using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes.Properties;
using ColorVision.UI;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Themes
{

    public class ThemeConfigSetingProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            ComboBox cmtheme = new ComboBox() { SelectedValuePath = "Key", DisplayMemberPath = "Value" };
            cmtheme.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedValueProperty, new Binding(nameof(ThemeConfig.Theme)));

            cmtheme.ItemsSource = from e1 in Enum.GetValues(typeof(Theme)).Cast<Theme>()
                                  select new KeyValuePair<Theme, string>(e1, Resources.ResourceManager.GetString(e1.ToDescription(), CultureInfo.CurrentUICulture) ?? "");

            cmtheme.SelectionChanged += (s, e) => Application.Current.ApplyTheme(ThemeConfig.Instance.Theme);
            cmtheme.DataContext = ThemeConfig.Instance;

            return new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    Name = Resources.Theme,
                    Description = Resources.Theme,
                    Type = ConfigSettingType.ComboBox,
                    BindingName = nameof(ThemeConfig.Theme),
                    Source = ThemeConfig.Instance,
                    ComboBox = cmtheme
                },
                new ConfigSettingMetadata
                {
                    Name = Resources.TransparentWindow,
                    Description = Resources.TransparentWindow,
                    Type = ConfigSettingType.Bool,
                    BindingName = nameof(ThemeConfig.TransparentWindow),
                    Source = ThemeConfig.Instance,
                }
            };
        }
    }


    public class ThemeConfig: ViewModelBase,IConfig
    {
        public static ThemeConfig Instance => ConfigService.Instance.GetRequiredService<ThemeConfig>();

        /// <summary>
        /// 主题
        /// </summary>
        public Theme Theme { get; set; } = Theme.UseSystem;

        public bool TransparentWindow { get => _TransparentWindow; set { _TransparentWindow = value; NotifyPropertyChanged(); } }
        private bool _TransparentWindow = true;
    }
}
