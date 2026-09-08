using ColorVision.Themes;
using ColorVision.UI.Menus;
using Spectrum.Help.Art;
using Spectrum.Help;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Spectrum.Tests;

public sealed class SpectrumAboutWindowTests
{
    [Fact]
    public void AboutEntryBelongsToSpectrumHelp()
    {
        var menu = new MenuSpectrumAbout();
        Assert.Equal("Spectrum", menu.TargetName);
        Assert.Equal(MenuItemConstants.Help, menu.OwnerGuid);
    }

    [Fact]
    public void StandaloneAboutShowsPluginVersionAndSwitchesOnlyItsOwnOpaquePalette()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                try
                {
                    foreach (string culture in new[] { "zh-CN", "en-US", "zh-TW" })
                    {
                        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                        foreach (Theme theme in new[] { Theme.Light, Theme.Dark })
                        {
                            app.ApplyTheme(theme);
                            var window = new SpectrumAboutWindow { ShowActivated = false, Left = -10000, Top = -10000 };
                            try
                            {
                                window.Show();
                                PumpDispatcher();
                                Assert.Equal(typeof(SpectrumAboutWindow).Assembly.GetName().Version!.ToString(), ((TextBlock)window.FindName("VersionLabel")).Text);
                                var scene = (AboutArtScene)window.FindName("SpectralScene");
                                Assert.Equal(AboutArtwork.Spectrum, scene.Artwork);
                                Assert.Equal(theme == Theme.Dark, scene.IsDark);
                                Assert.Equal(AboutText.SpectrumTitle, window.Title);
                                Assert.Equal(AboutText.SpectrumTitle, new MenuSpectrumAbout().Header);
                                Assert.Equal(AboutText.SpectrumHeadline, ((TextBlock)window.FindName("HeadlineLabel")).Text);
                                if (culture == "en-US")
                                {
                                    Assert.Equal("About Spectrum", window.Title);
                                    Assert.Equal("Every wavelength. Infinite possibility.", ((TextBlock)window.FindName("HeadlineLabel")).Text);
                                    Assert.Equal("Close · Esc", ((Button)window.FindName("CloseButton")).ToolTip);
                                }
                                Assert.False(window.IsBlurEnabled);
                                Color initialColor = ((SolidColorBrush)window.Background).Color;
                                Assert.Equal(255, initialColor.A);
                                ((Button)window.FindName("PaletteButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                Assert.Equal(theme != Theme.Dark, scene.IsDark);
                                Assert.Equal(theme, ThemeManager.Current.CurrentUITheme);
                                Assert.Equal(theme, ThemeManager.Current.CurrentTheme);
                                Assert.Equal(255, ((SolidColorBrush)window.Background).Color.A);
                                if (!SystemParameters.HighContrast) Assert.NotEqual(initialColor, ((SolidColorBrush)window.Background).Color);
                            }
                            finally { window.Close(); PumpDispatcher(); }

                            var reopened = new SpectrumAboutWindow { ShowActivated = false, Left = -10000, Top = -10000 };
                            try
                            {
                                reopened.Show();
                                PumpDispatcher();
                                Assert.Equal(theme == Theme.Dark, ((AboutArtScene)reopened.FindName("SpectralScene")).IsDark);
                            }
                            finally { reopened.Close(); PumpDispatcher(); }
                        }
                    }
                }
                finally { app.Shutdown(); }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
