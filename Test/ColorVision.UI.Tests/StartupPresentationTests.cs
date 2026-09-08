using ColorVision.Startup;
using ColorVision.Themes;
using ColorVision.UI;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class StartupPresentationTests
{
    private static readonly MethodInfo StartupContentRendered = typeof(StartWindow).GetMethod(
        "StartWindow_ContentRendered", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("The startup handler must be found and detached before showing a UI-only test window.");
    private static readonly FieldInfo ThemeSubscribers = typeof(ThemeManager).GetField(
        nameof(ThemeManager.CurrentUIThemeChanged), BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("CurrentUIThemeChanged backing field was not found.");

    [Theory]
    [InlineData("zh-CN", Theme.Light, Theme.Dark)]
    [InlineData("zh-CN", Theme.Dark, Theme.Light)]
    [InlineData("en-US", Theme.Light, Theme.Dark)]
    [InlineData("en-US", Theme.Dark, Theme.Light)]
    [InlineData("zh-TW", Theme.Light, Theme.Dark)]
    [InlineData("zh-TW", Theme.Dark, Theme.Light)]
    [InlineData("en-US", Theme.UseSystem, Theme.Light)]
    [InlineData("en-US", Theme.UseSystem, Theme.Dark)]
    public void RealSplashUsesResolvedThemeAndLocalizedCopyWithoutStartingTheApplication(string cultureName, Theme selection, Theme appsTheme)
        => RunSplash(cultureName, selection, appsTheme, StartupTheme.FollowApplication);

    [Theory]
    [InlineData(null, Theme.Light)]
    [InlineData(StartupTheme.Light, Theme.Dark)]
    public void DefaultDarkAndExplicitLightRemainIndependentOfApplicationTheme(StartupTheme? startupSelection, Theme applicationTheme)
        => RunSplash("en-US", applicationTheme, Theme.Light, startupSelection);

    private static void RunSplash(string cultureName, Theme selection, Theme appsTheme, StartupTheme? startupSelection)
    {
        WpfTestHost.Invoke(() =>
        {
            Application application = Application.Current;
            ResourceDictionary previousResources = application.Resources;
            Window? previousMainWindow = application.MainWindow;
            ThemeManager previousManager = ThemeManager.Current;
            IConfigService? previousConfig = ConfigService.Instance;
            CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
            CultureInfo previousFormatCulture = CultureInfo.CurrentCulture;
            var manager = new ThemeManager { AppsTheme = appsTheme };
            StartWindow? window = null;
            try
            {
                application.Resources = new ResourceDictionary();
                ThemeManager.Current = manager;
                var config = new StartupThemeTestConfigService();
                if (startupSelection.HasValue) config.Theme.StartupTheme = startupSelection.Value;
                ConfigService.SetInstance(config);
                CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                manager.ApplyTheme(application, selection);
                Theme resolvedApplication = selection == Theme.UseSystem ? appsTheme : selection;
                Theme resolved = config.Theme.StartupTheme switch
                {
                    StartupTheme.Light => Theme.Light,
                    StartupTheme.FollowApplication => resolvedApplication,
                    _ => Theme.Dark
                };
                int subscribersBefore = SubscriberCount(manager);

                window = CreateUiOnlyWindow();
                AssertPalette(window, resolved);
                Assert.Equal(subscribersBefore + 1, SubscriberCount(manager));
                window.Show();
                window.UpdateLayout();
                PumpDispatcher();

                Assert.True(window.IsLoaded);
                AssertPalette(window, resolved);
                Assert.Empty(Descendants(window).OfType<ButtonBase>());
                var headline = Assert.IsType<TextBlock>(window.FindName("headlineText"));
                Assert.Contains(headline.Text, StartupText.Headlines);
                Assert.DoesNotMatch(@"[。.]$", headline.Text);
                Assert.Equal(StartupText.Title, window.Title);
                Assert.Equal(StartupText.PreparingWorkspace, Assert.IsType<TextBlock>(window.FindName("startupStatusText")).Text);
                Assert.Equal(StartupText.Capabilities, Assert.IsType<TextBlock>(window.FindName("capabilitiesText")).Text);
                var progress = Assert.IsType<ProgressBar>(window.FindName("startupProgressBar"));
                Assert.Equal(StartupText.ProgressAutomationName, AutomationProperties.GetName(progress));
                if (cultureName == "en-US")
                {
                    AssertNoChinese(window.Title);
                    AssertNoChinese(AutomationProperties.GetName(progress));
                    foreach (TextBlock text in Descendants(window).OfType<TextBlock>()) AssertNoChinese(text.Text);
                    AssertEnglishCopyFits(window);
                }

                progress.Maximum = 100;
                progress.Value = 37;
                Theme changedTheme = resolvedApplication == Theme.Dark ? Theme.Light : Theme.Dark;
                manager.ApplyTheme(application, changedTheme);
                PumpDispatcher();
                Assert.Equal(changedTheme, manager.CurrentUITheme);
                AssertPalette(window, startupSelection == StartupTheme.FollowApplication ? changedTheme : resolved);
                Assert.Equal(100d, progress.Maximum);
                Assert.Equal(37d, progress.Value);

                Theme nextTheme = resolved == Theme.Dark ? Theme.Light : Theme.Dark;
                if (startupSelection != StartupTheme.FollowApplication)
                {
                    config.Theme.StartupTheme = nextTheme == Theme.Dark ? StartupTheme.Dark : StartupTheme.Light;
                    PumpDispatcher();
                    AssertPalette(window, resolved);
                }
                window.Close();
                window = null;
                PumpDispatcher();
                Assert.Equal(subscribersBefore, SubscriberCount(manager));

                if (startupSelection != StartupTheme.FollowApplication)
                {
                    // A saved selection is consumed by the next startup window, without recoloring one already open.
                    window = CreateUiOnlyWindow();
                    AssertPalette(window, nextTheme);
                    window.Show();
                    window.UpdateLayout();
                    PumpDispatcher();
                    AssertPalette(window, nextTheme);
                    window.Close();
                    window = null;
                    PumpDispatcher();
                    Assert.Equal(subscribersBefore, SubscriberCount(manager));
                }
            }
            finally
            {
                try { window?.Close(); PumpDispatcher(); }
                finally
                {
                    manager.Dispose();
                    ThemeManager.Current = previousManager;
                    ConfigService.SetInstance(previousConfig!);
                    application.Resources = previousResources;
                    application.MainWindow = previousMainWindow;
                    CultureInfo.CurrentUICulture = previousUiCulture;
                    CultureInfo.CurrentCulture = previousFormatCulture;
                }
            }
        });
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("zh-TW")]
    [InlineData("en-US")]
    public void StartupHeadlinePoolIsLocalizedDistinctAndPunctuationFree(string cultureName)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            IReadOnlyList<string> headlines = StartupText.Headlines;
            Assert.Equal(6, headlines.Count);
            Assert.Equal(headlines.Count, headlines.Distinct(StringComparer.Ordinal).Count());
            Assert.All(headlines, headline =>
            {
                Assert.False(string.IsNullOrWhiteSpace(headline));
                Assert.DoesNotMatch(@"[。.]$", headline);
                if (cultureName == "en-US") AssertNoChinese(headline);
                else Assert.Matches(@"[\u3400-\u9FFF]", headline);
            });
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("zh-TW")]
    [InlineData("en-US")]
    public void EveryStartupStageUsesLocalizedPresentationIncludingUnknownExtensions(string cultureName)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            var stages = new Dictionary<string, string>
            {
                ["MySqlInitializer"] = StartupText.ConnectingServices,
                ["MqttInitializer"] = StartupText.ConnectingServices,
                ["RCInitializer"] = StartupText.ConnectingServices,
                ["SocketInitializer"] = StartupText.ConnectingServices,
                ["SolutionManagerInitializer"] = StartupText.LoadingWorkspace,
                ["TemplateInitializer"] = StartupText.LoadingTemplates,
                ["ServiceInitializer"] = StartupText.PreparingDevicePanels,
                ["CudaInitializer"] = StartupText.PreparingCompute,
                ["UnknownPluginInitializer"] = StartupText.LoadingExtensions
            };
            foreach ((string initializer, string expected) in stages)
            {
                Assert.Equal(expected, StartupText.GetStage(initializer));
                Assert.False(string.IsNullOrWhiteSpace(expected));
                if (cultureName == "en-US") AssertNoChinese(expected);
                else Assert.Matches(@"[\u3400-\u9FFF]", expected);
            }
            Assert.Equal(cultureName switch
            {
                "zh-CN" => "正在打开工作空间",
                "zh-TW" => "正在打開工作空間",
                _ => "Opening your workspace"
            }, StartupText.OpeningWorkspace);
            if (cultureName == "en-US") Assert.Equal("Loading extensions", StartupText.GetStage("UnknownPluginInitializer"));
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    private static StartWindow CreateUiOnlyWindow()
    {
        var window = new StartWindow();
        try
        {
            // Never call App startup, initializers, service discovery, or the device chain in UI tests.
            // Delegate creation fails before Show if the production event signature changes.
            window.ContentRendered -= StartupContentRendered.CreateDelegate<EventHandler>(window);
            window.ShowActivated = false;
            window.ShowInTaskbar = false;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -10000;
            window.Top = -10000;
            return window;
        }
        catch { window.Close(); throw; }
    }

    private static void AssertPalette(StartWindow window, Theme theme)
    {
        Assert.Equal(theme == Theme.Dark, Assert.IsType<StartupScene>(window.FindName("startupScene")).IsDark);
        Color expected = SystemParameters.HighContrast ? SystemColors.WindowColor
            : theme == Theme.Dark ? Color.FromRgb(8, 14, 25) : Color.FromRgb(244, 245, 247);
        Assert.Equal(expected, Assert.IsType<SolidColorBrush>(window.Background).Color);
        Assert.Equal(expected, Assert.IsType<SolidColorBrush>(Assert.IsType<Grid>(window.FindName("StartupRoot")).Background).Color);
    }

    private static void AssertEnglishCopyFits(StartWindow window)
    {
        var root = Assert.IsType<Grid>(window.FindName("StartupRoot"));
        var headline = Assert.IsType<TextBlock>(window.FindName("headlineText"));
        var status = Assert.IsType<TextBlock>(window.FindName("startupStatusText"));
        var capabilities = Assert.IsType<TextBlock>(window.FindName("capabilitiesText"));
        // The template phase is longer than the initial English status and must still clear the right column.
        status.Text = StartupText.LoadingTemplates;
        foreach (string candidate in StartupText.Headlines)
        {
            headline.Text = candidate;
            window.UpdateLayout();
            Rect headlineBounds = TextBounds(headline, root);
            Rect statusBounds = TextBounds(status, root);
            Rect capabilitiesBounds = TextBounds(capabilities, root);
            Assert.True(statusBounds.Right + 19 <= capabilitiesBounds.Left, "English startup status overlaps the capabilities column.");
            Assert.True(headlineBounds.Bottom <= statusBounds.Top, "The English headline overlaps the status row.");
            foreach (Rect bounds in new[] { headlineBounds, statusBounds, capabilitiesBounds })
                Assert.True(bounds.Right <= root.ActualWidth - 37, "English splash text extends beyond the content margin.");
        }
    }

    private static Rect TextBounds(TextBlock text, Visual ancestor)
    {
        var measured = new TextBlock
        {
            Text = text.Text, FontFamily = text.FontFamily, FontSize = text.FontSize,
            FontWeight = text.FontWeight, FontStyle = text.FontStyle, FontStretch = text.FontStretch
        };
        TextOptions.SetTextFormattingMode(measured, TextOptions.GetTextFormattingMode(text));
        measured.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.True(measured.DesiredSize.Width <= text.ActualWidth + 1, $"{text.Name} clips its English text horizontally.");
        Assert.True(measured.DesiredSize.Height <= text.ActualHeight + 1, $"{text.Name} clips its English text vertically.");
        return new Rect(text.TransformToAncestor(ancestor).Transform(new Point()), measured.DesiredSize);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }

    private static int SubscriberCount(ThemeManager manager) =>
        (ThemeSubscribers.GetValue(manager) as MulticastDelegate)?.GetInvocationList().Length ?? 0;

    private static void AssertNoChinese(string text) => Assert.DoesNotMatch(@"[\u3400-\u4DBF\u4E00-\u9FFF\uF900-\uFAFF]", text);

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
