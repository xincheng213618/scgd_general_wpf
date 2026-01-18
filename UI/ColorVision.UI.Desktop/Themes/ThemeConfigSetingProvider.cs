using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.Themes.Properties;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.UI.Desktop.Themes
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
                }
            };
        }
    }
}
