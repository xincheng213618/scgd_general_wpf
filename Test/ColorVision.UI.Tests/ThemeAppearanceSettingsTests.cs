using ColorVision.Themes;
using ColorVision.UI.Desktop.Settings;
using Newtonsoft.Json;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class ThemeAppearanceSettingsTests
{
    // Existing configs retain the shipped defaults; edited light/dark profiles
    // are persisted independently instead of serializing the current UI facade.
    [Fact]
    public void ExistingConfigAndEditedProfilesRoundTripWithoutChangingTheOtherMode()
    {
        WpfTestHost.Invoke(() =>
        {
            ThemeManager previous = ThemeManager.Current;
            ResourceDictionary resources = Application.Current.Resources;
            using var manager = new ThemeManager();
            ThemeManager.Current = manager;
            Application.Current.Resources = new ResourceDictionary();
            try
            {
                var config = JsonConvert.DeserializeObject<ThemeConfig>("{}")!;
                Assert.Equal(ThemePreset.Default, config.LightAppearance.Preset);
                Assert.Equal(ThemePreset.Default, config.DarkAppearance.Preset);
                config.Theme = Theme.Light;
                config.Preset = ThemePreset.Codex;
                config.AccentColor = "#8347CD";
                config.Theme = Theme.Dark;
                config.Preset = ThemePreset.Warm;
                config.BackgroundColor = "#282522";
                string json = JsonConvert.SerializeObject(config);
                Assert.DoesNotContain("AccentColor", json);
                var restored = JsonConvert.DeserializeObject<ThemeConfig>(json)!;
                restored.Apply();
                Assert.Equal("#8347CD", restored.LightAppearance.Accent);
                Assert.Equal(ThemePreset.Warm, restored.DarkAppearance.Preset);
                Assert.Equal(Color.FromRgb(40, 37, 34), Application.Current.Resources["CV.Color.Surface.Window"]);
                restored.Theme = Theme.Light;
                restored.Apply();
                Assert.Equal("#8347CD", restored.AccentColor);
                restored.ResetAppearanceCommand.Execute(null);
                Assert.Equal(Theme.UseSystem, restored.Theme);
                Assert.Null(restored.LightAppearance.Accent);
                Assert.Null(restored.DarkAppearance.Background);
                Assert.Equal(ThemePreset.Default, restored.LightAppearance.Preset);
            }
            finally { Application.Current.Resources = resources; ThemeManager.Current = previous; }
        });
    }

    [Fact]
    public void ThemeMetadataCreatesAnIndependentAppearancePageAndSearchKeepsTheSameIdentity()
    {
        WpfTestHost.Invoke(() =>
        {
            ThemeManager previous = ThemeManager.Current;
            ResourceDictionary resources = Application.Current.Resources;
            using var manager = new ThemeManager();
            ThemeManager.Current = manager;
            Application.Current.Resources = new ResourceDictionary();
            SettingWindow? window = null;
            try
            {
                var config = new ThemeConfig { Theme = Theme.Light };
                config.Apply();
                var metadata = typeof(ThemeConfig).GetProperties().Select(property => (property, attr: property.GetCustomAttribute<ConfigSettingAttribute>()))
                    .Where(item => item.attr != null).Select(item => new ConfigSettingMetadata
                    {
                        Group = item.attr!.Group, Section = item.attr.Section, Name = item.attr.Name, Description = item.attr.Description,
                        Order = item.attr.Order, Layout = item.attr.Layout, BindingName = item.property.Name, Source = config
                    }).ToList();
                var constructor = typeof(SettingWindow).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(IEnumerable<ConfigSettingMetadata>)], null)!;
                window = (SettingWindow)constructor.Invoke([metadata]);
                var navigation = (ListBox)window.FindName("NavigationListBox");
                Assert.Equal("Appearance", Assert.Single(navigation.Items.Cast<object>()).GetType().GetProperty("Group")!.GetValue(navigation.Items[0]));
                string id = $"setting:Property:{typeof(ThemeConfig).Assembly.GetName().Name}:{typeof(ThemeConfig).FullName}:Theme";
                Assert.True(window.NavigateToSetting(id));
                Assert.NotEmpty(((StackPanel)window.FindName("SettingsContentPanel")).Children.Cast<UIElement>());
                Assert.Equal(ConfigSettingConstants.Universal, typeof(ColorVision.UI.Languages.LanguageConfig)
                    .GetProperty("UICulture")!.GetCustomAttribute<ConfigSettingAttribute>()!.Group);
            }
            finally { window?.Close(); Application.Current.Resources = resources; ThemeManager.Current = previous; }
        });
    }

}
