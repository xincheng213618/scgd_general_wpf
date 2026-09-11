using ColorVision.Themes;
using ColorVision.UI.Views.About;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class AboutMsgWindowTests
{
    private static readonly FieldInfo ThemeSubscribers = typeof(ThemeManager).GetField(
        nameof(ThemeManager.CurrentUIThemeChanged), BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("CurrentUIThemeChanged backing field was not found.");

    [Theory]
    [InlineData(Theme.Light, Theme.Dark, Theme.Light)]
    [InlineData(Theme.Dark, Theme.Light, Theme.Dark)]
    [InlineData(Theme.UseSystem, Theme.Light, Theme.Light)]
    [InlineData(Theme.UseSystem, Theme.Dark, Theme.Dark)]
    public void EachOpeningStartsFromTheResolvedApplicationThemeInsteadOfThePreviousLocalPalette(Theme requestedTheme, Theme systemAppTheme, Theme expectedTheme)
    {
        WpfTestHost.Invoke(() =>
        {
            Application application = Application.Current;
            ThemeManager previousManager = ThemeManager.Current;
            Window? previousMainWindow = application.MainWindow;
            ResourceDictionary[] previousResources = application.Resources.MergedDictionaries.ToArray();
            var manager = new ThemeManager { AppsTheme = systemAppTheme };
            AboutMsgWindow? first = null;
            AboutMsgWindow? reopened = null;
            try
            {
                // This manager and Application belong to the test process; no persisted settings are changed.
                ThemeManager.Current = manager;
                manager.ApplyTheme(application, requestedTheme);
                Assert.Equal(expectedTheme, manager.CurrentUITheme);
                ResourceDictionary[] applicationPalette = application.Resources.MergedDictionaries.ToArray();
                int subscribersBefore = SubscriberCount(manager);
                bool expectedDark = expectedTheme == Theme.Dark;

                first = CreateOffscreenWindow();
                var firstScene = Assert.IsType<AboutArtScene>(first.FindName("SpectralScene"));
                Assert.Equal(expectedDark, firstScene.IsDark);
                first.Show();
                first.UpdateLayout();
                PumpDispatcher();
                Assert.Equal(expectedDark, firstScene.IsDark);
                Assert.Equal(subscribersBefore + 1, SubscriberCount(manager));
                Color initialSurface = Assert.IsType<SolidColorBrush>(first.Background).Color;

                var palette = Assert.IsType<Button>(first.FindName("PaletteButton"));
                palette.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, palette));
                PumpDispatcher();
                Assert.Equal(!expectedDark, firstScene.IsDark);
                if (!SystemParameters.HighContrast)
                    Assert.NotEqual(initialSurface, Assert.IsType<SolidColorBrush>(first.Background).Color);
                Assert.Same(manager, ThemeManager.Current);
                Assert.Equal(requestedTheme, manager.CurrentTheme);
                Assert.Equal(expectedTheme, manager.CurrentUITheme);
                Assert.Equal(systemAppTheme, manager.AppsTheme);
                Assert.Equal(applicationPalette, application.Resources.MergedDictionaries);

                first.Close();
                first = null;
                PumpDispatcher();
                Assert.Equal(subscribersBefore, SubscriberCount(manager));

                reopened = CreateOffscreenWindow();
                var reopenedScene = Assert.IsType<AboutArtScene>(reopened.FindName("SpectralScene"));
                Assert.Equal(expectedDark, reopenedScene.IsDark);
                reopened.Show();
                reopened.UpdateLayout();
                PumpDispatcher();
                Assert.Equal(expectedDark, reopenedScene.IsDark);
                Assert.Equal(initialSurface, Assert.IsType<SolidColorBrush>(reopened.Background).Color);
                Assert.Equal(requestedTheme, manager.CurrentTheme);
                Assert.Equal(expectedTheme, manager.CurrentUITheme);
                reopened.Close();
                reopened = null;
                PumpDispatcher();
                Assert.Equal(subscribersBefore, SubscriberCount(manager));
            }
            finally
            {
                try
                {
                    first?.Close();
                    reopened?.Close();
                    PumpDispatcher();
                }
                finally
                {
                    manager.Dispose();
                    ThemeManager.Current = previousManager;
                    application.Resources.MergedDictionaries.Clear();
                    foreach (ResourceDictionary resource in previousResources)
                        application.Resources.MergedDictionaries.Add(resource);
                    application.MainWindow = previousMainWindow;
                }
            }
        });
    }

    private static AboutMsgWindow CreateOffscreenWindow() => new()
    {
        ShowActivated = false,
        ShowInTaskbar = false,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Left = -10000,
        Top = -10000
    };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndCloseWindow(string cultureName, string expectedHeadline)
    {
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo previousFormatCulture = CultureInfo.CurrentCulture;
        Application application = Application.Current;
        Window? previousMainWindow = application.MainWindow;
        ThemeManager publisher = ThemeManager.Current;
        Theme? selectedTheme = publisher.CurrentTheme;
        Theme resolvedTheme = publisher.CurrentUITheme;
        int subscribersBefore = SubscriberCount(publisher);
        AboutMsgWindow? window = null;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            // Exercise the product BAML and templates, including values rejected only at runtime.
            window = new AboutMsgWindow
            {
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -10000,
                Top = -10000
            };
            window.Show();
            window.UpdateLayout();
            PumpDispatcher();

            Assert.True(window.IsLoaded);
            var headline = Assert.IsType<TextBlock>(window.FindName("HeadlineLabel"));
            Assert.Equal(expectedHeadline, headline.Text);
            if (cultureName is "en-US" or "de-DE")
            {
                AssertNoChinese(window.Title);
                foreach (DependencyObject visual in Descendants(window))
                {
                    if (visual is TextBlock text) AssertNoChinese(text.Text);
                    if (visual is not Button button) continue;
                    AssertNoChinese(Assert.IsType<string>(button.ToolTip));
                    string accessibleName = AutomationProperties.GetName(button);
                    Assert.False(string.IsNullOrWhiteSpace(accessibleName));
                    AssertNoChinese(accessibleName);
                }
                AssertEnglishCopyFits(window);
            }
            Assert.Equal(subscribersBefore + 1, SubscriberCount(publisher));
            var version = Assert.IsType<TextBlock>(window.FindName("VersionLabel"));
            Assert.Equal(typeof(AboutMsgWindow).Assembly.GetName().Version?.ToString(), version.Text);
            Assert.False(window.AllowsTransparency);
            Assert.False(window.IsBlurEnabled);
            Assert.Equal(1d, window.Opacity);
            Color surfaceBefore = Assert.IsType<SolidColorBrush>(window.Background).Color;
            Assert.Equal((byte)255, surfaceBefore.A);

            var scene = Assert.IsType<AboutArtScene>(window.FindName("SpectralScene"));
            Assert.NotNull(PresentationSource.FromVisual(scene));
            bool darkBefore = scene.IsDark;
            var palette = Assert.IsType<Button>(window.FindName("PaletteButton"));
            palette.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, palette));
            PumpDispatcher();

            Assert.Equal(!darkBefore, scene.IsDark);
            Color surfaceAfter = Assert.IsType<SolidColorBrush>(window.Background).Color;
            Assert.Equal((byte)255, surfaceAfter.A);
            if (SystemParameters.HighContrast)
                Assert.Equal(SystemColors.WindowColor, surfaceAfter);
            else
                Assert.NotEqual(surfaceBefore, surfaceAfter);
            Assert.Same(publisher, ThemeManager.Current);
            Assert.Equal(selectedTheme, publisher.CurrentTheme);
            Assert.Equal(resolvedTheme, publisher.CurrentUITheme);
            Assert.Equal(previousFormatCulture, CultureInfo.CurrentCulture);
            Assert.Equal(cultureName, CultureInfo.CurrentUICulture.Name);
        }
        finally
        {
            try
            {
                window?.Close();
                PumpDispatcher();
            }
            finally
            {
                application.MainWindow = previousMainWindow;
                CultureInfo.CurrentUICulture = previousUiCulture;
                CultureInfo.CurrentCulture = previousFormatCulture;
            }
        }

        Assert.Same(previousMainWindow, application.MainWindow);
        Assert.Equal(subscribersBefore, SubscriberCount(publisher));
        Assert.DoesNotContain(window!, application.Windows.Cast<Window>());
        return new WeakReference(window);
    }

    private static void AssertEnglishCopyFits(AboutMsgWindow window)
    {
        var panel = Assert.IsType<StackPanel>(window.FindName("IdentityPanel"));
        Assert.True(panel.ActualWidth > 0);
        foreach (string name in new[] { "DescriptorLabel", "HeadlineLabel", "CapabilitiesLabel" })
        {
            var label = Assert.IsType<TextBlock>(window.FindName(name));
            double availableWidth = panel.ActualWidth - label.Margin.Left - label.Margin.Right;
            Assert.True(label.DesiredSize.Width <= panel.ActualWidth + 1, $"{name} exceeds the identity panel's measured width.");
            var unconstrained = new TextBlock
            {
                Text = label.Text,
                FontFamily = label.FontFamily,
                FontSize = label.FontSize,
                FontWeight = label.FontWeight,
                FontStyle = label.FontStyle,
                FontStretch = label.FontStretch
            };
            TextOptions.SetTextFormattingMode(unconstrained, TextOptions.GetTextFormattingMode(label));
            unconstrained.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.True(unconstrained.DesiredSize.Width <= availableWidth + 1, $"{name} clips its English text: {unconstrained.DesiredSize.Width:F1} DIP needs more than {availableWidth:F1} DIP.");
            Assert.True(label.ActualHeight + 1 >= unconstrained.DesiredSize.Height, $"{name} clips its text vertically.");
        }
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

    private static void AssertNoChinese(string value) => Assert.DoesNotMatch(@"[\u3400-\u4DBF\u4E00-\u9FFF\uF900-\uFAFF]", value);

    private static int SubscriberCount(ThemeManager publisher) =>
        (ThemeSubscribers.GetValue(publisher) as MulticastDelegate)?.GetInvocationList().Length ?? 0;

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
