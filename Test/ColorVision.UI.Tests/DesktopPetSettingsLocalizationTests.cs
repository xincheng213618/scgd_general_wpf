using ColorVision.FloatingBall;
using ColorVision.Themes;
using ColorVision.UI;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class DesktopPetSettingsLocalizationTests
{
    [Theory]
    [InlineData("zh-Hans", false, "创建宠物", "小彩", "选择", "已选择")]
    [InlineData("en-US", false, "Create pet", "Xiaocai", "Select", "Selected")]
    [InlineData("zh-Hant", false, "建立寵物", "小彩", "選擇", "已選擇")]
    [InlineData("zh-Hans", true, "创建宠物", "小彩", "选择", "已选择")]
    [InlineData("en-US", true, "Create pet", "Xiaocai", "Select", "Selected")]
    [InlineData("zh-Hant", true, "建立寵物", "小彩", "選擇", "已選擇")]
    public void SettingsAndCreationUseLocalizedResourcesAndWrapCards(
        string culture, bool dark, string create, string name, string select, string selected)
    {
        WpfTestHost.Invoke(() =>
        {
            var previous = CultureInfo.CurrentUICulture;
            var previousConfig = ConfigService.Instance;
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            var previousDictionaries = dictionaries.ToArray();
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
                dictionaries.Clear();
                foreach (var uri in (dark ? ThemeManager.ResourceDictionaryDark : ThemeManager.ResourceDictionaryWhite).Concat(ThemeManager.ResourceDictionaryBase))
                    dictionaries.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) });

                var control = new DesktopPetSettingsControl();
                var model = Assert.IsType<DesktopPetSettingsViewModel>(control.DataContext);
                var defaultOption = new DesktopPetAssetOption(DesktopPetAssetCatalog.Shared.Assets[0]) { IsSelected = true };
                var codexOption = new DesktopPetAssetOption(new DesktopPetAsset(
                    "codex-builtin:dewey", "Dewey", "Raw built-in description", DesktopPetAssetSource.CodexBuiltIn, 2));
                var customAsset = new DesktopPetAsset(
                    "colorvision-custom:test", "My 原始名称", "My 原始描述", DesktopPetAssetSource.ColorVisionCustom, 2);
                var customOption = new DesktopPetAssetOption(customAsset);
                model.Assets.Add(defaultOption);
                model.Assets.Add(codexOption);
                model.Assets.Add(customOption);
                control.Measure(new Size(800, double.PositiveInfinity));
                control.Arrange(new Rect(0, 0, 800, control.DesiredSize.Height));
                control.UpdateLayout();

                Assert.Equal(name, defaultOption.DisplayName);
                Assert.Equal(selected, defaultOption.SelectButtonText);
                Assert.False(defaultOption.CanSelect);
                Assert.Equal(select, customOption.SelectButtonText);
                Assert.Equal(customAsset.DisplayName, customOption.DisplayName);
                Assert.Equal(customAsset.Description, customOption.Description);
                Assert.Equal(DesktopPetText.DeweyDescription, codexOption.Description);
                Assert.Equal(DesktopPetText.SourceCodexBuiltIn, codexOption.SourceLabel);
                Assert.Equal(DesktopPetText.FollowCopilot, Descendants<CheckBox>(control).First().Content is TextBlock text ? text.Text : null);
                Assert.Contains(Descendants<Button>(control), button => Equals(button.Content, create));
                var scale = Assert.Single(Descendants<Slider>(control));
                Assert.Equal(1, scale.Value);
                scale.Value = 1.2;
                Assert.Equal(1.2, model.Config.PetScale);
                var followCopilot = Descendants<CheckBox>(control).First();
                Assert.True(followCopilot.IsChecked);
                followCopilot.IsChecked = false;
                Assert.False(model.Config.EnableCopilotIntegration);
                Assert.False(Descendants<CheckBox>(control).Last().IsEnabled);

                control.Measure(new Size(800, double.PositiveInfinity));
                control.Arrange(new Rect(0, 0, 800, control.DesiredSize.Height));
                control.UpdateLayout();
                var wrap = Assert.Single(Descendants<WrapPanel>(control), panel => panel.Children.OfType<ContentPresenter>().Any());
                var wideRows = wrap.Children.Cast<UIElement>().Select(child => child.TranslatePoint(new Point(), wrap).Y).Distinct().Count();
                Assert.Equal(1, wideRows);
                control.Measure(new Size(540, double.PositiveInfinity));
                control.Arrange(new Rect(0, 0, 540, control.DesiredSize.Height));
                control.UpdateLayout();
                var narrowRows = wrap.Children.Cast<UIElement>().Select(child => child.TranslatePoint(new Point(), wrap).Y).Distinct().Count();
                Assert.Equal(2, narrowRows);
                Assert.DoesNotContain(Descendants<ScrollViewer>(control), viewer => viewer.Content is StackPanel);

                var creation = new DesktopPetCreateWindow();
                Assert.Equal(DesktopPetText.CreateTitle, creation.Title);
                var creationContent = Assert.IsType<Grid>(creation.Content);
                creationContent.Measure(new Size(660, 650));
                creationContent.Arrange(new Rect(0, 0, 660, 650));
                creationContent.UpdateLayout();
                var tabs = Assert.Single(Descendants<TabControl>(creationContent));
                Assert.Equal(DesktopPetText.CreateWithCodex, Assert.IsType<TabItem>(tabs.Items[0]).Header);
                Assert.Equal(DesktopPetText.ImportSpriteSheet, Assert.IsType<TabItem>(tabs.Items[1]).Header);
                var property = typeof(DesktopPetConfig).GetProperty(nameof(DesktopPetConfig.EnableCopilotIntegration))!;
                var manager = PropertyEditorHelper.GetResourceManager(typeof(DesktopPetConfig));
                Assert.NotEqual("ConfigDesktopPetCopilotIntegration", PropertyEditorHelper.GetDisplayName(manager, property));
                Assert.NotEqual("ConfigDesktopPetCopilotIntegrationDescription", PropertyEditorHelper.GetDescription(manager, property));
                creation.Close();
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
                ConfigService.SetInstance(previousConfig);
                dictionaries.Clear();
                foreach (var dictionary in previousDictionaries)
                    dictionaries.Add(dictionary);
            }
        });
    }

    [Theory]
    [InlineData("zh-Hans", "请选择精灵表文件。")]
    [InlineData("en-US", "Choose a sprite sheet file.")]
    [InlineData("zh-Hant", "請選擇精靈圖集檔案。")]
    public async Task ImportValidationUsesTheSelectedLanguage(string culture, string expected)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => DesktopPetPackageService.ImportAsync(
                new DesktopPetImportRequest("Test", "", "", 2)));
            Assert.StartsWith(expected, exception.Message);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
                yield return match;
            foreach (var descendant in Descendants<T>(child))
                yield return descendant;
        }
    }
}
