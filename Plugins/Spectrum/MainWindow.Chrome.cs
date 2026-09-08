using ColorVision.Themes;
using ColorVision.Windowing;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace Spectrum;

public partial class MainWindow
{
    private ThemeManager? windowThemeManager;
    private CompactTitleBarChrome? compactTitleBar;
    private MainWindowConfig? chromeConfig;
    private Thickness ordinaryMenuMargin;
    private VerticalAlignment ordinaryMenuAlignment;

    private void InitializeCompactTitleBar()
    {
        if (!Config.UseCompactTitleBar || !CompactTitleBarChrome.IsSupportedOperatingSystem)
        {
            this.ApplyCaption();
            return;
        }

        ordinaryMenuMargin = menu.Margin;
        ordinaryMenuAlignment = menu.VerticalAlignment;
        chromeConfig = Config;
        // This subscription must precede SetWindowFull: suspend WindowChrome before
        // the shared full-screen helper changes the native window style.
        chromeConfig.PropertyChanged += CompactTitleBarConfigChanged;
        SourceInitialized += AttachCompactTitleBar;
    }

    private void AttachCompactTitleBar(object? sender, EventArgs e)
    {
        SourceInitialized -= AttachCompactTitleBar;
        var chrome = new CompactTitleBarChrome(this, MainWindowTitleBar, NativeCaptionButtonsPlaceholder, Root);
        try
        {
            if (chrome.TryAttach())
            {
                compactTitleBar = chrome;
                SetCompactHeaderAlignment(true);
                chrome.ApplyTheme(windowThemeManager?.CurrentUITheme == Theme.Dark);
                log.Info("Spectrum 紧凑标题栏已接入原生窗口按钮。");
                return;
            }
        }
        catch (Exception ex)
        {
            log.Warn("Spectrum 紧凑标题栏初始化失败，保留原生标题栏。", ex);
        }

        chrome.Dispose();
        compactTitleBar = null;
        SetCompactHeaderAlignment(false);
        this.ApplyCaption();
    }

    private void SetCompactHeaderAlignment(bool compact)
    {
        menu.VerticalAlignment = compact ? VerticalAlignment.Center : ordinaryMenuAlignment;
        menu.Margin = compact
            ? new Thickness(ordinaryMenuMargin.Left, ordinaryMenuMargin.Top + 4, ordinaryMenuMargin.Right, ordinaryMenuMargin.Bottom)
            : ordinaryMenuMargin;
        CompactDragRegion.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CompactTitleBarConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowConfig.IsFull) || compactTitleBar == null)
            return;

        if (chromeConfig!.IsFull)
        {
            compactTitleBar.SetFullScreen(true);
            SetCompactHeaderAlignment(false);
        }
        else
        {
            _ = Dispatcher.BeginInvoke(() =>
            {
                if (disposed || chromeConfig?.IsFull != false)
                    return;
                compactTitleBar?.SetFullScreen(false);
                SetCompactHeaderAlignment(compactTitleBar?.IsAttached == true);
            }, DispatcherPriority.Loaded);
        }
    }

    private void DisposeWindowAppearance()
    {
        SourceInitialized -= AttachCompactTitleBar;
        if (windowThemeManager != null)
            windowThemeManager.CurrentUIThemeChanged -= ApplyDockTheme;
        windowThemeManager = null;
        if (chromeConfig != null)
            chromeConfig.PropertyChanged -= CompactTitleBarConfigChanged;
        chromeConfig = null;
        compactTitleBar?.Dispose();
        compactTitleBar = null;
    }
}
