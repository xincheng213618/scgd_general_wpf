using ColorVision.UI.Views.About;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace ColorVision.UI.Tests;

public sealed class AboutTextTests
{
    private const string ResourceName = "ColorVision.UI.Views.About.AboutResources";
    private static readonly string[] ExpectedKeys =
    [
        nameof(AboutText.AboutCaption), nameof(AboutText.PaletteTooltip), nameof(AboutText.PaletteAutomation),
        nameof(AboutText.CloseTooltip), nameof(AboutText.CloseAutomation), nameof(AboutText.VisionDescriptor),
        nameof(AboutText.VisionHeadline), nameof(AboutText.VisionSubline), nameof(AboutText.VisionCapabilities),
        nameof(AboutText.SpectrumTitle), nameof(AboutText.SpectrumEyebrow), nameof(AboutText.SpectrumHeadline),
        nameof(AboutText.SpectrumSubline), nameof(AboutText.MainAboutTitle)
    ];

    [Theory]
    [InlineData("")]
    [InlineData("en")]
    [InlineData("zh-Hant")]
    public void EveryPublishedPropertyHasANonEmptyEntryInEachCompiledResource(string resourceCulture)
    {
        var manager = new ResourceManager(ResourceName, typeof(AboutText).Assembly);
        try
        {
            // No parent fallback: missing satellite entries must fail instead of borrowing Chinese text.
            ResourceSet resources = Assert.IsAssignableFrom<ResourceSet>(manager.GetResourceSet(CultureInfo.GetCultureInfo(resourceCulture), true, false));
            string[] actualKeys = resources.Cast<DictionaryEntry>().Select(entry => (string)entry.Key).Order().ToArray();
            Assert.Equal(ExpectedKeys.Order(), actualKeys);
            Assert.Equal(ExpectedKeys.Order(), typeof(AboutText).GetProperties(BindingFlags.Static | BindingFlags.Public).Select(property => property.Name).Order());
            foreach (string key in ExpectedKeys)
            {
                string value = Assert.IsType<string>(resources.GetObject(key));
                Assert.False(string.IsNullOrWhiteSpace(value), $"{resourceCulture}/{key} is empty.");
                Assert.NotEqual(key, value);
                if (resourceCulture == "en") Assert.DoesNotMatch(@"[\u3400-\u4DBF\u4E00-\u9FFF\uF900-\uFAFF]", value);
            }
        }
        finally
        {
            manager.ReleaseAllResources();
        }
    }

    [Theory]
    [InlineData("zh-CN", "", "让视觉，成为判断。")]
    [InlineData("zh-SG", "", "让视觉，成为判断。")]
    [InlineData("zh-Hant", "zh-Hant", "讓視覺，成為判斷。")]
    [InlineData("zh-TW", "zh-Hant", "讓視覺，成為判斷。")]
    [InlineData("zh-HK", "zh-Hant", "讓視覺，成為判斷。")]
    [InlineData("zh-MO", "zh-Hant", "讓視覺，成為判斷。")]
    [InlineData("en-US", "en", "Turn vision into insight.")]
    [InlineData("de-DE", "en", "Turn vision into insight.")]
    [InlineData("fr-FR", "en", "Turn vision into insight.")]
    [InlineData("ja-JP", "en", "Turn vision into insight.")]
    [InlineData("ar-SA", "en", "Turn vision into insight.")]
    public void UiCultureSelectsTheCompletePresentationWithoutChangingAnyCulture(string uiCulture, string resourceCulture, string expectedHeadline)
    {
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo originalFormatCulture = CultureInfo.CurrentCulture;
        var manager = new ResourceManager(ResourceName, typeof(AboutText).Assembly);
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(uiCulture);
            // Deliberately disagree with the UI culture to catch routing based on number/date formatting.
            CultureInfo formatCulture = CultureInfo.GetCultureInfo(resourceCulture == "en" ? "zh-CN" : "de-DE");
            CultureInfo.CurrentCulture = formatCulture;
            ResourceSet resources = Assert.IsAssignableFrom<ResourceSet>(manager.GetResourceSet(CultureInfo.GetCultureInfo(resourceCulture), true, false));
            foreach (string key in ExpectedKeys)
            {
                PropertyInfo property = Assert.IsAssignableFrom<PropertyInfo>(typeof(AboutText).GetProperty(key));
                Assert.Equal(resources.GetString(key), Assert.IsType<string>(property.GetValue(null)));
            }
            Assert.Equal(expectedHeadline, AboutText.VisionHeadline);
            Assert.Equal(uiCulture, CultureInfo.CurrentUICulture.Name);
            Assert.Equal(formatCulture, CultureInfo.CurrentCulture);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUiCulture;
            CultureInfo.CurrentCulture = originalFormatCulture;
            manager.ReleaseAllResources();
        }
    }
}
