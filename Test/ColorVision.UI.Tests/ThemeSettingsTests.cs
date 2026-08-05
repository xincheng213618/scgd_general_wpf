using ColorVision.Themes;
using ColorVision.UI;

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
    }
}
