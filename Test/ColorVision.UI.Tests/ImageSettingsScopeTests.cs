using ColorVision.Engine.Media;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Ruler;
using ColorVision.ImageEditor.EditorTools.Filters;
using ColorVision.ImageEditor.EditorTools.PseudoColor;
using ColorVision.ImageEditor.Settings;
using ColorVision.UI;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ImageSettingsScopeTests
{
    [Fact]
    public void ShaderEditsStayInTheirViewUntilExplicitlySavedAsDefaults() => Run(service =>
    {
        using ImageView first = new();
        using ImageView second = new();
        var a = first.IEditorToolFactory.GetIEditorTool<DisplayShaderFilterEditorTool>()!;
        var b = second.IEditorToolFactory.GetIEditorTool<DisplayShaderFilterEditorTool>()!;
        double original = b.State.Brightness;
        a.State.Brightness = 0.25;
        a.Save();
        Assert.Equal(original, b.State.Brightness);
        Assert.Equal(original, DisplayShaderFilterDefaultConfig.Current.State.Brightness);
        Assert.Empty(service.SavedTypes);
        a.SaveAsDefault();
        using ImageView third = new();
        Assert.Equal(0.25, third.IEditorToolFactory.GetIEditorTool<DisplayShaderFilterEditorTool>()!.State.Brightness);
        Assert.Equal(original, b.State.Brightness);
        Assert.Equal(new[] { typeof(DisplayShaderFilterDefaultConfig) }, service.SavedTypes);
    });

    [Fact]
    public void ExplicitShaderPersistenceStillBelongsToTheExternalOwner() => Run(service =>
    {
        using ImageView view = new();
        var shader = view.IEditorToolFactory.GetIEditorTool<DisplayShaderFilterEditorTool>()!;
        DisplayShaderFilterState external = new() { Brightness = 0.2 };
        int saves = 0;
        shader.AttachPersistence(external, () => saves++);
        Assert.Equal(0.2, shader.State.Brightness);
        shader.State.Brightness = 0.3;
        shader.Save();
        Assert.Equal(0.3, external.Brightness);
        Assert.Equal(1, saves);
        Assert.Empty(service.SavedTypes);
    });

    [Fact]
    public void CalibrationIsSharedInsideOneViewButIsolatedAcrossViews() => Run(service =>
    {
        using ImageView first = new();
        using ImageView second = new();
        first.Config.SetViewState(ImageCalibrationService.CalibrationSourceKeyProperty, "camera-a");
        second.Config.SetViewState(ImageCalibrationService.CalibrationSourceKeyProperty, "camera-b");
        ImageCalibrationService.ApplyToView(first.Config);
        ImageCalibrationService.ApplyToView(second.Config);
        Assert.Single(ImageCalibrationConfig.Instance.Profiles); // Reading an unknown source does not create a saved profile.
        DrawingVisualScaleHost scale = new(first.Config) { ActualLength = 0.2, PhysicalUnit = "mm", IsUsePhysicalUnit = true };
        DrawingVisualRuler ruler = new();
        first.EditorContext.DrawCanvas.AddVisual(ruler);
        Assert.Same(first.Config.Calibration, ruler.Calibration);
        Assert.Equal(0.2, ruler.EffectiveActualLength);
        Assert.Equal("mm", ruler.EffectivePhysicalUnit);
        Assert.Throws<ArgumentException>(() => second.EditorContext.DrawCanvas.AddVisual(ruler));
        Assert.Same(first.Config.Calibration, ruler.Calibration);
        Assert.Equal(1, second.Config.Calibration.ActualLength);
        Assert.False(second.Config.Calibration.IsUsePhysicalUnit);
        Assert.Empty(service.SavedTypes);
        ImageCalibrationService.SaveCurrent(first.Config);
        Assert.Equal(0.2, ImageCalibrationConfig.Instance.ReadProfile("camera-a").ActualLength);
        Assert.Equal(1, ImageCalibrationConfig.Instance.ReadProfile("camera-b").ActualLength);
        Assert.Equal(new[] { typeof(ImageCalibrationConfig) }, service.SavedTypes);
        GC.KeepAlive(scale);
    });

    [Fact]
    public void CalibrationReloadAndSourceSwitchAreExplicit() => Run(_ =>
    {
        using ImageView view = new();
        view.Config.Calibration.ActualLength = 0.1;
        ImageCalibrationService.ApplyToView(view.Config);
        Assert.Equal(0.1, view.Config.Calibration.ActualLength);
        ImageCalibrationService.ApplyToView(view.Config, reload: true);
        Assert.Equal(1, view.Config.Calibration.ActualLength);
        view.Config.Calibration.ActualLength = 0.2;
        view.Config.SetImageMetadata(ImageViewPropertyKeys.CameraModel, "camera");
        ImageCalibrationService.ApplyToView(view.Config);
        Assert.Equal(1, view.Config.Calibration.ActualLength);
        Assert.StartsWith("Camera:", view.Config.CalibrationProfileKey);
    });

    [Fact]
    public void ProbePreferencesSurviveImageChangesWithoutChangingGlobalDefaults() => Run(_ =>
    {
        using ImageView first = new();
        using ImageView second = new();
        var a = CvcieMouseProbeOptions.GetOrCreate(first);
        var b = CvcieMouseProbeOptions.GetOrCreate(second);
        a.Radius = 23;
        first.Config.ClearProperties();
        Assert.Same(a, CvcieMouseProbeOptions.GetOrCreate(first));
        Assert.Equal(23, a.Radius);
        Assert.NotEqual(a.Radius, b.Radius);
        Assert.NotEqual(a.Radius, CvcieMouseProbeOptions.CurrentDefaults.Radius);
    });

    [Fact]
    public void ImageChangesPreservePseudoPreferencesAndRecomputeTheRange() => Run(_ =>
    {
        using ImageView view = new();
        var pseudo = view.IEditorToolFactory.GetIEditorTool<PseudoColorEditorTool>()!;
        pseudo.State.IsAutoSetRange = false;
        pseudo.State.ColormapTypes = ColorVision.Core.ColormapTypes.COLORMAP_JET;
        view.SetImageSource(new WriteableBitmap(2, 2, 96, 96, PixelFormats.Gray16, null));
        Assert.False(pseudo.State.IsAutoSetRange);
        Assert.Equal(ColorVision.Core.ColormapTypes.COLORMAP_JET, pseudo.State.ColormapTypes);
        Assert.Equal(65535, pseudo.State.SliderValueEnd);
        view.SetImageSource(new WriteableBitmap(2, 2, 96, 96, PixelFormats.Gray8, null));
        Assert.Equal(255, pseudo.State.SliderValueEnd);
        Assert.False(pseudo.State.IsEnabled);
    });

    [Fact]
    public void SessionSavesOnlyChangedPersistentTargetsAndRetriesOnlyFailures() => Run(_ =>
    {
        DisplayShaderFilterState first = new(), second = new(), current = new();
        int firstSaves = 0, secondSaves = 0, legacySaves = 0;
        bool fail = true;
        using ImageSettingsSession session = new(new[] {
            new ImageViewSettingsEntry("a", "a", first, () => firstSaves++) { Scope = ImageSettingsScope.Defaults },
            new ImageViewSettingsEntry("b", "b", second, () => { if (fail) throw new IOException("disk unavailable"); secondSaves++; }) { Scope = ImageSettingsScope.Application },
            new ImageViewSettingsEntry("legacy", "legacy", current, () => legacySaves++)
        });
        current.Brightness = 0.5;
        Assert.False(session.HasChanges);
        first.Brightness = 0.2;
        first.Brightness = 0;
        Assert.False(session.HasChanges);
        first.Brightness = second.Brightness = 0.3;
        Assert.Single(session.SaveChanged());
        Assert.Equal(1, firstSaves);
        Assert.True(session.HasChanges);
        fail = false;
        Assert.Empty(session.SaveChanged());
        Assert.False(session.HasChanges);
        Assert.Equal(1, firstSaves);
        Assert.Equal(1, secondSaves);
        Assert.Equal(0, legacySaves);
    });

    [Fact]
    public void ClosingAnUnchangedWindowDoesNotPersistCurrentViewOrDefaults() => Run(service =>
    {
        using ImageView view = new();
        ImageViewSettingsWindow window = new(view, ImageSettingsCategories.Information);
        view.Config.IsShowText = false;
        view.Config.Calibration.ActualLength = 0.4;
        window.Close();
        Assert.Empty(service.SavedTypes);
    });

    [Fact]
    public void WindowSaveThenCloseDoesNotSaveTwice() => Run(service =>
    {
        using ImageView view = new();
        ImageViewSettingsWindow window = new(view);
        DisplayShaderFilterDefaultConfig.Current.State.Brightness = 0.3;
        ((Button)window.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.Close();
        Assert.Equal(new[] { typeof(DisplayShaderFilterDefaultConfig) }, service.SavedTypes);
    });

    [Fact]
    public void SaveFailureKeepsTheWindowOpenForRetry() => Run(service =>
    {
        using ImageView view = new();
        ImageViewSettingsWindow window = new(view);
        bool closed = false;
        window.Closed += (_, _) => closed = true;
        service.FailWrites = true;
        DisplayShaderFilterDefaultConfig.Current.State.Brightness = 0.3;
        window.Close();
        Assert.False(closed);
        Assert.Contains("disk unavailable", ((TextBlock)window.FindName("StatusText")).Text);
        service.FailWrites = false;
        window.Close();
        Assert.True(closed);
        Assert.Single(service.SavedTypes);
    });

    [Fact]
    public void ProviderFailureDoesNotHideOtherEntriesAndRegistrationCanBeRemoved() => Run(_ =>
    {
        using ImageView view = new();
        using IDisposable broken = view.RegisterSettingsProvider(() => throw new InvalidOperationException("unavailable"));
        ImageViewSettingsEntry entry = new("extension", "one", new object()) { Id = "test-entry" };
        IDisposable registration = view.RegisterSettingsProvider(() => new[] { entry });
        Assert.Contains(entry, view.GetRegisteredSettings());
        registration.Dispose();
        registration.Dispose();
        Assert.DoesNotContain(entry, view.GetRegisteredSettings());
    });

    [Fact]
    public void SavingAReplacedConfigurationDoesNotWriteADifferentObject() => Run(service =>
    {
        var source = DisplayShaderFilterDefaultConfig.Current;
        ConfigService.SetInstance(new RecordingConfigService());
        Assert.Throws<InvalidOperationException>(() => ImageSettingsPersistence.Save(source));
        Assert.Empty(service.SavedTypes);
    });

    [Fact]
    public void AllSettingsPagesCanBeBuiltAndBooleanLabelsAreNotDuplicated() => Run(_ =>
    {
        using ImageView view = new();
        new CvcieDisplaySettingProvider().Execute(view);
        new CvcieMouseProbeSettingProvider().Execute(view);
        ImageViewSettingsWindow window = new(view);
        try
        {
            var list = (ListBox)window.FindName("SettingsList");
            foreach (object page in list.Items)
            {
                list.SelectedItem = page;
                var content = (ContentControl)window.FindName("SettingsContent");
                Assert.DoesNotContain(LogicalDescendants(content).OfType<TextBlock>(), text => text.Text.StartsWith(SettingsText.Unavailable));
            }
            ((TextBox)window.FindName("SearchBox")).Text = SettingsText.IsLayoutUpdated;
            var selectedContent = (ContentControl)window.FindName("SettingsContent");
            Assert.Single(LogicalDescendants(selectedContent).OfType<TextBlock>(), text => text.Text == SettingsText.IsLayoutUpdated);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void SuccessfulDefaultActionDoesNotQueueAnExtraSaveOnClose() => Run(service =>
    {
        using ImageView view = new();
        ImageViewSettingsWindow window = new(view, ImageSettingsCategories.Filters);
        var shader = view.IEditorToolFactory.GetIEditorTool<DisplayShaderFilterEditorTool>()!;
        shader.State.Brightness = 0.6;
        Button action = Assert.Single(LogicalDescendants(window).OfType<Button>(), button => Equals(button.Content, SettingsText.SetAsDefault));
        action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.Close();
        Assert.Equal(0.6, DisplayShaderFilterDefaultConfig.Current.State.Brightness);
        Assert.Equal(new[] { typeof(DisplayShaderFilterDefaultConfig) }, service.SavedTypes);
    });

    private static IEnumerable<DependencyObject> LogicalDescendants(DependencyObject parent)
    {
        foreach (DependencyObject child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child;
            foreach (DependencyObject descendant in LogicalDescendants(child)) yield return descendant;
        }
    }

    private static void Run(Action<RecordingConfigService> action) => WpfTestHost.Invoke(() =>
    {
        IConfigService? previous = ConfigService.Instance;
        try
        {
            ConfigService.SetInstance(new RecordingConfigService());
            var resources = Application.Current.Resources;
            foreach (string key in new[] { "GlobalTextBrush", "PrimaryTextBrush" }) resources[key] = Brushes.Black;
            foreach (string key in new[] { "ButtonBorderBrush", "BorderBrush", "SecondaryTextBrush" }) resources[key] = Brushes.Gray;
            foreach (string key in new[] { "GlobalBorderBrush", "GlobalBackground", "ButtonBackground" }) resources[key] = Brushes.White;
            resources["TextBox.Small"] = new Style(typeof(TextBox));
            resources["ComboBox.Small"] = new Style(typeof(ComboBox));
            resources["ButtonDefault"] = new Style(typeof(Button));
            resources["ButtonCommand"] = new Style(typeof(Button));
            resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
            resources["ToolBarImage"] = new Style(typeof(Image));
            resources["BaseStyle"] = new Style(typeof(Control));
            resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
            resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
            action((RecordingConfigService)ConfigService.Instance);
        }
        finally { ConfigService.SetInstance(previous!); }
    });

    private sealed class RecordingConfigService : IConfigService
    {
        private readonly ConfigHandler _defaults = new();
        public List<Type> SavedTypes { get; } = new();
        public bool FailWrites { get; set; }
        public IConfig GetRequiredService(Type type) => _defaults.GetRequiredService(type);
        public T GetRequiredService<T>() where T : IConfig => _defaults.GetRequiredService<T>();
        public void Save<T>() where T : IConfig { if (FailWrites) throw new IOException("disk unavailable"); SavedTypes.Add(typeof(T)); }
        public void SaveConfigs() { }
        public void LoadConfigs() { }
    }
}
