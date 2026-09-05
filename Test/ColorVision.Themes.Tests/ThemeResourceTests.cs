using ColorVision.Themes;
using ColorVision.Themes.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;
using System.Reflection;
using System.Windows.Input;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ColorVision.Themes.Tests;

public class ThemeResourceTests
{
    private static readonly Lazy<Dispatcher> Dispatcher = new(() =>
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            ready.Set();
            System.Windows.Threading.Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return dispatcher!;
    });

    private static void Run(Action<Application, ThemeManager> action) => Dispatcher.Value.Invoke(() =>
    {
        var app = Application.Current;
        var saved = app.Resources;
        app.Resources = new ResourceDictionary();
        using var manager = new ThemeManager();
        try { action(app, manager); }
        finally { app.Resources = saved; }
    });

    [Fact]
    public void FirstLightApplicationInitializesResourcesAndSwitchingKeepsOneGroup() => Run((app, manager) =>
    {
        manager.ApplyTheme(app, Theme.Light);
        Assert.Equal(Colors.White, ((SolidColorBrush)app.FindResource("GlobalBackground")).Color);
        var first = app.Resources.MergedDictionaries.Single();
        manager.ApplyTheme(app, Theme.Light);
        Assert.Same(first, app.Resources.MergedDictionaries.Single());
        for (int i = 0; i < 12; i++)
        {
            var theme = i % 2 == 0 ? Theme.Dark : Theme.Light;
            manager.ApplyThemeChanged(app, theme);
            Assert.Single(app.Resources.MergedDictionaries);
            Assert.Equal(theme == Theme.Dark ? Color.FromRgb(38, 38, 38) : Colors.White, ((SolidColorBrush)app.FindResource("GlobalBackground")).Color);
        }
    });

    [Fact]
    public async Task WorkerApplicationsNotifyOnTheApplicationDispatcher()
    {
        var state = Dispatcher.Value.Invoke(() =>
        {
            var app = Application.Current;
            var saved = app.Resources;
            app.Resources = new ResourceDictionary();
            var manager = new ThemeManager();
            manager.CurrentUIThemeChanged += _ => Assert.True(app.Dispatcher.CheckAccess());
            return (app, saved, manager);
        });
        try
        {
            await Task.Run(() => state.manager.ApplyTheme(state.app, Theme.Dark));
            Assert.Equal(Theme.Dark, state.manager.CurrentUITheme);
        }
        finally
        {
            Dispatcher.Value.Invoke(() => { state.manager.Dispose(); state.app.Resources = state.saved; });
        }
    }

    [Fact]
    public void LegacyStartupIsAdoptedWithoutRemovingHostOverrides() => Run((app, manager) =>
    {
        var before = new ResourceDictionary { ["Host.Before"] = 42 };
        var after = new ResourceDictionary { ["GlobalTextBrush"] = Brushes.Magenta };
        app.Resources.MergedDictionaries.Add(before);
        foreach (string path in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(path, UriKind.Relative) });
        app.Resources.MergedDictionaries.Add(after);
        manager.ApplyTheme(app, Theme.Dark);
        Assert.Equal(3, app.Resources.MergedDictionaries.Count);
        Assert.Same(before, app.Resources.MergedDictionaries[0]);
        Assert.Same(after, app.Resources.MergedDictionaries[2]);
        Assert.Same(Brushes.Magenta, app.FindResource("GlobalTextBrush"));
        Assert.Equal(42, app.FindResource("Host.Before"));
    });

    [Fact]
    public void UnifiedXamlEntryCanBeReplacedAndAnotherManagerCanAdoptIt() => Run((app, manager) =>
    {
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) });
        Assert.IsType<Style>(app.FindResource(typeof(Button)));
        manager.ApplyTheme(app, Theme.Dark);
        using var next = new ThemeManager();
        next.ApplyTheme(app, Theme.Light);
        Assert.Single(app.Resources.MergedDictionaries);
    });

    [Fact]
    public void FailedLoadingPreservesActiveResourcesSelectionAndNotifications() => Run((app, manager) =>
    {
        manager.ApplyTheme(app, Theme.Light);
        var active = app.Resources.MergedDictionaries.Single();
        int notifications = 0;
        manager.CurrentThemeChanged += _ => notifications++;
        manager.CurrentUIThemeChanged += _ => notifications++;
        var original = ThemeManager.ResourceDictionaryDark;
        try
        {
            ThemeManager.ResourceDictionaryDark = [.. original, "/ColorVision.Themes;component/Themes/DoesNotExist.xaml"];
            Assert.ThrowsAny<Exception>(() => manager.ApplyTheme(app, Theme.Dark));
            Assert.Same(active, app.Resources.MergedDictionaries.Single());
            Assert.Equal(Theme.Light, manager.CurrentTheme);
            Assert.Equal(Theme.Light, manager.CurrentUITheme);
            Assert.Equal(0, notifications);
        }
        finally { ThemeManager.ResourceDictionaryDark = original; }
        manager.ApplyTheme(app, Theme.Dark);
        Assert.Equal(Theme.Dark, manager.CurrentUITheme);
        Assert.Equal(2, notifications);
    });

    [Fact]
    public void SystemPolicyAndForceRefreshPublishResolvedResources() => Run((app, manager) =>
    {
        manager.AppsTheme = Theme.Light;
        manager.ApplyTheme(app, Theme.UseSystem);
        manager.CurrentUIThemeChanged += theme =>
        {
            Assert.Equal(theme, manager.CurrentUITheme);
            Assert.Equal(Theme.UseSystem, manager.CurrentTheme);
            Assert.Equal(theme == Theme.Dark ? Colors.White : Colors.Black, ((SolidColorBrush)app.FindResource("GlobalTextBrush")).Color);
        };
        manager.AppsTheme = Theme.Dark;
        Assert.Equal(Theme.Dark, manager.CurrentUITheme);
        manager.ApplyThemeChanged(app, Theme.UseSystem);
        Assert.Equal(Theme.Dark, manager.CurrentUITheme);
        Assert.Single(app.Resources.MergedDictionaries);
    });

    [Theory]
    [InlineData(Theme.Light)]
    [InlineData(Theme.Dark)]
    public void CommonControlsLoadAndRenderWithRealTemplates(Theme theme) => Run((app, manager) =>
    {
        manager.ApplyTheme(app, theme);
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "ColorVision — 主题控件预览", FontSize = 24, Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new Button { Content = "普通操作", Margin = new Thickness(0, 4, 0, 4) });
        panel.Children.Add(new Button { Content = "禁用操作", IsEnabled = false, Margin = new Thickness(0, 4, 0, 4) });
        panel.Children.Add(new TextBox { Text = "输入内容 / Enter", Margin = new Thickness(0, 4, 0, 4) });
        var combo = new ComboBox { Margin = new Thickness(0, 4, 0, 4) };
        combo.Items.Add("选项一"); combo.Items.Add("选项二"); combo.SelectedIndex = 0;
        panel.Children.Add(combo);
        panel.Children.Add(new ColorVision.Themes.Controls.ToggleSwitch { Content = "开关", IsChecked = true });
        panel.Children.Add(new Slider { Style = (Style)app.FindResource("UpDownSlider"), Minimum = 0, Maximum = 100, Value = 42 });
        panel.Children.Add(new Image { Source = (ImageSource)app.FindResource("DrawingImageSave"), Width = 24, Height = 24 });
        var menu = new Menu();
        menu.Items.Add(new MenuItem { Header = "文件(_F)" });
        menu.Items.Add(new MenuItem { Header = "帮助(_H)" });
        panel.Children.Add(menu);
        var window = new Window { Content = panel, Width = 600, Height = 480, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
        window.SetResourceReference(Control.BackgroundProperty, "GlobalBackground");
        window.SetResourceReference(Control.ForegroundProperty, "GlobalTextBrush");
        try
        {
            window.Show();
            window.UpdateLayout();
            Assert.All(panel.Children.OfType<Control>(), control => Assert.NotNull(control.Template));
            string? output = Environment.GetEnvironmentVariable("COLORVISION_THEME_PREVIEW");
            if (output != null)
            {
                Directory.CreateDirectory(output);
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(output, $"{theme}.png")); encoder.Save(stream);
            }
        }
        finally { window.Close(); }
    });

    [Fact]
    public void LightAndDarkExposeTheSameKeysAndTypesAndLegacyDefaults() => Run((app, manager) =>
    {
        Dictionary<object, Type> ReadTheme(Theme theme)
        {
            manager.ApplyTheme(app, theme);
            var result = new Dictionary<object, Type>();
            void Read(ResourceDictionary dictionary)
            {
                foreach (var child in dictionary.MergedDictionaries)
                    if (child.Source?.OriginalString.Contains("HandyControl;") != true) Read(child);
                foreach (object key in dictionary.Keys)
                    result[key] = dictionary[key].GetType();
            }
            Read(app.Resources.MergedDictionaries.Single());
            foreach (var (legacy, current) in new[] {
                ("GlobalTextColor", "CV.Color.Text.Primary"), ("GlobalBackgroundColor", "CV.Color.Surface.Window"),
                ("UpdateDialogAccentColor", "CV.Color.Accent.Primary"), ("UpdateDialogBorderColor", "CV.Color.Border.Control") })
                Assert.Equal(app.FindResource(legacy), app.FindResource(current));
            return result;
        }
        var light = ReadTheme(Theme.Light);
        var dark = ReadTheme(Theme.Dark);
        Assert.Equal(light.Count, dark.Count);
        foreach (var item in light) Assert.Equal(item.Value, dark[item.Key]);
    });

    [Theory]
    [InlineData(Theme.Light)]
    [InlineData(Theme.Dark)]
    public void LegacyDialogAndWindowEntriesStillResolve(Theme theme) => Run((app, manager) =>
    {
        manager.ApplyTheme(app, theme);
        var dialog = new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/UpdateDialogTheme.xaml", UriKind.Relative) };
        foreach (object key in dialog.Keys) Assert.NotNull(dialog[key]);
        Assert.IsType<Style>(dialog["UpdateDialogPrimaryButtonStyle"]);
        Assert.IsType<SolidColorBrush>(dialog["UpdateDialog.TextSecondary"]);
        var legacyList = new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Listview.xaml", UriKind.Relative) };
        Assert.IsType<Style>(legacyList["ListViewItemStyle"]);
        var window = new BaseWindow { Width = 300, Height = 200, Left = -10000, Top = -10000, ShowInTaskbar = false, Content = new TextBlock { Text = "窗口模板" } };
        try { window.Show(); window.UpdateLayout(); Assert.NotNull(window.Template); }
        finally { window.Close(); }
    });

    [Fact]
    public void OpenControlsAndDynamicIconsFollowThemeChanges() => Run((app, manager) =>
    {
        manager.ApplyTheme(app, Theme.Light);
        var text = new TextBlock { Text = "操作" };
        text.SetResourceReference(TextBlock.ForegroundProperty, "CV.Action.Foreground");
        var image = (DrawingImage)app.FindResource("DrawingImageSave");
        var imageControl = new Image();
        imageControl.SetResourceReference(Image.SourceProperty, "DrawingImageSave");
        var panel = new StackPanel(); panel.Children.Add(text); panel.Children.Add(imageControl);
        var window = new Window { Content = panel, Width = 200, Height = 200, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show(); window.UpdateLayout();
            Assert.Equal(Colors.Black, ((SolidColorBrush)text.Foreground).Color);
            manager.ApplyTheme(app, Theme.Dark);
            window.UpdateLayout();
            Assert.Equal(Colors.White, ((SolidColorBrush)text.Foreground).Color);
            Assert.NotSame(image, imageControl.Source);
            var geometry = ((DrawingGroup)((DrawingImage)imageControl.Source).Drawing).Children.OfType<GeometryDrawing>().First();
            Assert.Equal(Colors.White, ((SolidColorBrush)geometry.Brush).Color);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void CaptionUsesOneActualThemeSubscriptionAndReleasesOriginalPublisher() => Run((app, manager) =>
    {
        ThemeManager saved = ThemeManager.Current;
        var eventField = typeof(ThemeManager).GetField("CurrentUIThemeChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
        int Count() => ((Delegate?)eventField.GetValue(manager))?.GetInvocationList().Length ?? 0;
        ThemeManager.Current = manager;
        var window = new Window { Width = 200, Height = 200, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.ApplyCaption(false); window.ApplyCaption(false);
            window.Show(); window.ApplyCaption(false);
            Assert.Equal(1, Count());
            ThemeManager.Current = saved;
            window.Close();
            Assert.Equal(0, Count());
        }
        finally { window.Close(); ThemeManager.Current = saved; }
    });

    [Fact]
    public void InputBehaviorsRetainEnterNavigationAndCanBeDisabled() => Run((app, manager) =>
    {
        manager.ApplyTheme(app, Theme.Light);
        var input = new TextBox();
        var next = new Button { Content = "下一步" };
        var panel = new StackPanel(); panel.Children.Add(input); panel.Children.Add(next);
        var window = new Window { Content = panel, Width = 200, Height = 160, Left = -10000, Top = -10000, ShowInTaskbar = false };
        KeyEventArgs Raise(Key key)
        {
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), Environment.TickCount, key)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            input.RaiseEvent(args);
            return args;
        }
        try
        {
            window.Show(); window.UpdateLayout();
            Keyboard.Focus(input);
            Assert.True(Raise(Key.Enter).Handled);
            Assert.Same(next, Keyboard.FocusedElement);
            InputKeyboardNavigation.SetEnterMovesFocus(input, false);
            Keyboard.Focus(input);
            Assert.False(Raise(Key.Enter).Handled);
            Assert.Same(input, Keyboard.FocusedElement);
            InputKeyboardNavigation.SetNumberKeysOnly(input, true);
            Assert.False(Raise(Key.NumPad5).Handled);
            Assert.False(Raise(Key.Left).Handled);
            Assert.True(Raise(Key.F5).Handled);
            InputKeyboardNavigation.SetNumberKeysOnly(input, false);
            Assert.False(Raise(Key.F5).Handled);
        }
        finally { window.Close(); }
    });
}
