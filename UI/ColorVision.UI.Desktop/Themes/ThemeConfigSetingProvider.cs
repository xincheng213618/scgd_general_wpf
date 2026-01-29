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
            return new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    Type = ConfigSettingType.Property,
                    BindingName = nameof(ThemeConfig.Theme),
                    Source = ThemeConfig.Instance,
                }
            };
        }
    }
}
