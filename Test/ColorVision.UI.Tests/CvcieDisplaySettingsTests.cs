using ColorVision.Engine.Media;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Settings;
using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using EditorResources = ColorVision.ImageEditor.Properties.Resources;

namespace ColorVision.UI.Tests;

public sealed class CvcieDisplaySettingsTests
{
    [Fact]
    public void GlobalSettingsAreAvailableBeforeOpeningAnImageAndSharedAcrossViews()
    {
        WithIsolatedConfig(service =>
        {
            using ImageView first = CreateImageView();
            using ImageView second = CreateImageView();
            Assert.False(first.Config.GetProperties<bool>("IsCVCIE"));
            Assert.False(second.Config.GetProperties<bool>("IsCVCIE"));

            ImageViewSettingsEntry firstEntry = GetDisplayEntry(first);
            ImageViewSettingsEntry secondEntry = GetDisplayEntry(second);
            CvcieDisplayConfig config = Assert.IsType<CvcieDisplayConfig>(firstEntry.Source);
            Assert.Same(config, secondEntry.Source);
            Assert.Same(service.GetRequiredService<CvcieDisplayConfig>(), config);
            Assert.Equal(CvcieBrightnessMode.Auto, config.BrightnessMode);
            Assert.Equal(65535, config.ReferenceWhiteLuminance);

            config.EnableTrueColor = true;
            config.ReferenceWhiteLuminance = 203.5;
            Assert.True(((CvcieDisplayConfig)secondEntry.Source).EnableTrueColor);
            Assert.Equal(203.5, ((CvcieDisplayConfig)secondEntry.Source).ReferenceWhiteLuminance);

            Assert.NotNull(firstEntry.Save);
            firstEntry.Save();
            Assert.Equal(new[] { typeof(CvcieDisplayConfig) }, service.SavedTypes);
        });
    }

    [Fact]
    public void RegisteredCvcieSettingsAppearInsideTheExistingDefaultsPage()
    {
        WithIsolatedConfig(_ =>
        {
            using ImageView imageView = CreateImageView();
            ImageViewSettingsWindow window = new(imageView, EditorResources.Settings_GroupDefaults);
            try
            {
                ListBox settingsList = Assert.IsType<ListBox>(window.FindName("SettingsList"));
                object defaultsPage = Assert.Single(settingsList.Items.Cast<object>().Where(page => GetPageTitle(page) == EditorResources.Settings_GroupDefaults));
                Assert.Same(defaultsPage, settingsList.SelectedItem);

                ContentControl settingsContent = Assert.IsType<ContentControl>(window.FindName("SettingsContent"));
                ScrollViewer defaultsContent = Assert.IsType<ScrollViewer>(settingsContent.Content);
                TextBlock sectionTitle = Assert.Single(Descendants(defaultsContent).OfType<TextBlock>().Where(text => text.Text == "CVCIE 显示"));
                Assert.Equal(Visibility.Visible, sectionTitle.Visibility);
                Assert.Contains(Descendants(defaultsContent).OfType<TextBlock>(), text => text.Text == EditorResources.Settings_DefaultDisplayParams);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static ImageViewSettingsEntry GetDisplayEntry(ImageView imageView)
        => Assert.Single(imageView.GetRegisteredSettings().Where(entry => entry.Source is CvcieDisplayConfig));

    private static ImageView CreateImageView()
    {
        ImageView imageView = new();
        // A settings-only test does not load a toolbar visual tree.
        imageView.IEditorToolFactory.IEditorTools.Clear();
        return imageView;
    }

    private static string GetPageTitle(object page)
    {
        TextBlock label = new();
        label.SetBinding(TextBlock.TextProperty, new Binding("Header") { Source = page });
        return label.Text;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (DependencyObject child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child;
            foreach (DependencyObject descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static void WithIsolatedConfig(Action<RecordingConfigService> action)
    {
        WpfTestHost.Invoke(() =>
        {
            IConfigService? previous = ConfigService.Instance;
            try
            {
                RecordingConfigService service = new();
                ConfigService.SetInstance(service);
                EnsureResources();
                action(service);
            }
            finally
            {
                ConfigService.SetInstance(previous!);
            }
        });
    }

    private static void EnsureResources()
    {
        Application application = Application.Current!;
        application.Resources["GlobalTextBrush"] = Brushes.Black;
        application.Resources["GlobalBorderBrush"] = Brushes.Transparent;
        application.Resources["GlobalBackground"] = Brushes.White;
        application.Resources["BorderBrush"] = Brushes.Gray;
        application.Resources["PrimaryTextBrush"] = Brushes.Black;
        application.Resources["SecondaryTextBrush"] = Brushes.Gray;
        application.Resources["ButtonCommand"] = new Style(typeof(Button));
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }

    private sealed class RecordingConfigService : IConfigService
    {
        private readonly ConfigHandler _defaults = new();
        public List<Type> SavedTypes { get; } = [];
        public IConfig GetRequiredService(Type type) => _defaults.GetRequiredService(type);
        public T GetRequiredService<T>() where T : IConfig => _defaults.GetRequiredService<T>();
        public void Save<T>() where T : IConfig => SavedTypes.Add(typeof(T));
        public void SaveConfigs() { }
        public void LoadConfigs() { }
    }
}
