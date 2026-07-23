using ColorVision.Themes;
using ColorVision.UI;
using System.ComponentModel;
using System.Reflection;

namespace ColorVision.UI.Tests
{
    public class ThemeSettingsTests
    {
        [Fact]
        public void SupportedThemesContainOnlySystemLightAndDark()
        {
            Assert.Equal(new[] { Theme.UseSystem, Theme.Light, Theme.Dark }, ThemeManager.SupportedThemes);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        public void ThemeConfigNormalizesRemovedLegacyThemeValues(int legacyValue)
        {
            var config = new ThemeConfig
            {
                Theme = (Theme)legacyValue
            };

            Assert.Equal(Theme.UseSystem, config.Theme);
        }

        [Fact]
        public void ThemeSettingUsesWideCustomPropertyEditor()
        {
            PropertyInfo property = typeof(ThemeConfig).GetProperty(nameof(ThemeConfig.Theme))!;
            ConfigSettingAttribute setting = property.GetCustomAttribute<ConfigSettingAttribute>()!;
            PropertyEditorTypeAttribute editor = property.GetCustomAttribute<PropertyEditorTypeAttribute>()!;

            Assert.Equal(ConfigSettingLayout.Wide, setting.Layout);
            Assert.Equal(typeof(ThemePropertiesEditor), editor.EditorType);
        }
    }
}
