using ColorVision.Engine.Media;
using ColorVision.UI;
using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class EnumPropertiesEditorTests
{
    [Fact]
    public void CvcieSettingsShowEnableSwitchAndBrightnessLabelsAndPreserveSavedMode()
    {
        WpfTestHost.Invoke(() =>
        {
            CultureInfo previousCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
                EnsurePropertyEditorResources();
                CvcieDisplayConfig config = new();
                DockPanel enablePanel = PropertyEditorHelper.GenProperties(typeof(CvcieDisplayConfig).GetProperty(nameof(CvcieDisplayConfig.EnableTrueColor))!, config);
                ToggleButton enable = Assert.Single(enablePanel.Children.OfType<ToggleButton>());
                ComboBox brightness = CreateCombo(config, nameof(CvcieDisplayConfig.BrightnessMode));

                Assert.Equal("启用真彩显示", Assert.Single(enablePanel.Children.OfType<TextBlock>()).Text);
                Assert.False(TypeDescriptor.GetProperties(config)[nameof(CvcieDisplayConfig.DisplayMode)]!.IsBrowsable);
                Assert.Equal(new[] { "自动适配", "固定参考白亮度" }, GetItems(brightness).Select(item => item.Value));
                Assert.False(enable.IsChecked);
                Assert.Equal(CvcieBrightnessMode.Auto, brightness.SelectedValue);

                enable.IsChecked = true;
                brightness.SelectedValue = CvcieBrightnessMode.ReferenceWhite;
                enable.GetBindingExpression(ToggleButton.IsCheckedProperty)!.UpdateSource();
                brightness.GetBindingExpression(Selector.SelectedValueProperty)!.UpdateSource();

                Assert.True(config.EnableTrueColor);
                Assert.Equal(CvcieDisplayMode.Srgb, config.DisplayMode);
                Assert.Equal(CvcieBrightnessMode.ReferenceWhite, config.BrightnessMode);
                Assert.Equal("1", JsonConvert.SerializeObject(config.DisplayMode));
                Assert.Equal("1", JsonConvert.SerializeObject(config.BrightnessMode));
                Assert.DoesNotContain(nameof(CvcieDisplayConfig.EnableTrueColor), JsonConvert.SerializeObject(config));

                enable.IsChecked = false;
                enable.GetBindingExpression(ToggleButton.IsCheckedProperty)!.UpdateSource();
                Assert.Equal(CvcieDisplayMode.Source, config.DisplayMode);
            }
            finally
            {
                CultureInfo.CurrentUICulture = previousCulture;
            }
        });
    }

    [Fact]
    public void ExistingEnumNameResourcesTakePriorityOverDisplayAndDescriptionMetadata()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsurePropertyEditorResources();
            WithResources(new LabelResourceManager(new Dictionary<string, string>
            {
                [nameof(LabelMode.Display)] = "Existing display translation",
                // A translation equal to its resource key is still an explicit translation.
                [nameof(LabelMode.Description)] = nameof(LabelMode.Description),
            }), () =>
            {
                ComboBox combo = CreateCombo(new LabelConfig(), nameof(LabelConfig.Mode));

                Assert.Equal(new[] { "Existing display translation", "Description", "Plain" }, GetItems(combo).Select(item => item.Value));
            });
        });
    }

    [Fact]
    public void MissingEnumResourcesUseMetadataAndPreserveNullableSelection()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsurePropertyEditorResources();
            WithResources(new LabelResourceManager(new Dictionary<string, string>
            {
                ["Description label"] = "Localized description",
            }), () =>
            {
                LabelConfig config = new();
                ComboBox combo = CreateCombo(config, nameof(LabelConfig.OptionalMode));
                KeyValuePair<object?, string>[] items = GetItems(combo);

                Assert.Equal(new[] { string.Empty, "Display resource label", "Localized description", "Plain" }, items.Select(item => item.Value));
                Assert.Null(items[0].Key);
                Assert.Null(combo.SelectedValue);

                combo.SelectedValue = LabelMode.Description;
                combo.GetBindingExpression(Selector.SelectedValueProperty)!.UpdateSource();
                Assert.Equal(LabelMode.Description, config.OptionalMode);

                combo.SelectedItem = items[0];
                combo.GetBindingExpression(Selector.SelectedValueProperty)!.UpdateSource();
                Assert.Null(config.OptionalMode);
            });
        });
    }

    private static ComboBox CreateCombo(object config, string propertyName)
    {
        PropertyInfo property = config.GetType().GetProperty(propertyName)!;
        DockPanel panel = PropertyEditorHelper.GenProperties(property, config);
        return Assert.Single(panel.Children.OfType<ComboBox>());
    }

    private static KeyValuePair<object?, string>[] GetItems(ComboBox combo)
        => combo.Items.Cast<KeyValuePair<object?, string>>().ToArray();

    private static void WithResources(ResourceManager resources, Action action)
    {
        Type type = typeof(LabelConfig);
        bool hadPrevious = PropertyEditorHelper.ResourceManagerCache.TryGetValue(type, out Lazy<ResourceManager?>? previous);
        try
        {
            PropertyEditorHelper.GetResourceManager(type, resources);
            action();
        }
        finally
        {
            if (hadPrevious) PropertyEditorHelper.ResourceManagerCache[type] = previous!;
            else PropertyEditorHelper.ResourceManagerCache.TryRemove(type, out _);
        }
    }

    private static void EnsurePropertyEditorResources()
    {
        Application application = Application.Current!;
        application.Resources["GlobalTextBrush"] = Brushes.Black;
        application.Resources["GlobalBorderBrush"] = Brushes.Transparent;
        application.Resources["BorderBrush"] = Brushes.Gray;
        application.Resources["ButtonCommand"] = new Style(typeof(Button));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }

    private sealed class LabelConfig
    {
        public LabelMode Mode { get; set; }
        public LabelMode? OptionalMode { get; set; }
    }

    public enum LabelMode
    {
        [Display(Name = nameof(LabelResources.DisplayLabel), ResourceType = typeof(LabelResources))]
        [Description("Unused description")]
        Display,
        [Description("Description label")]
        Description,
        Plain,
    }

    public static class LabelResources
    {
        public static string DisplayLabel => "Display resource label";
    }

    private sealed class LabelResourceManager(IReadOnlyDictionary<string, string> values) : ResourceManager
    {
        public override string? GetString(string name, CultureInfo? culture) => values.TryGetValue(name, out string? value) ? value : null;
    }
}
