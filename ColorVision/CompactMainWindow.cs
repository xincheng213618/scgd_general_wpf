using ColorVision.Themes;
using ColorVision.Update;
using ColorVision.Windowing;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;

namespace ColorVision;

/// <summary>Optional native-button chrome over the existing main-window workspace.</summary>
public sealed class CompactMainWindow : MainWindow
{
    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(CompactMainWindow));
    private CompactTitleBarChrome? _compactTitleBar;
    private ThemeManager? _compactTitleBarThemeManager;
    private MainWindowConfig? _compactTitleBarConfig;
    private bool _compactTitleBarClosed;
    private Thickness _ordinaryDockingMargin;
    private Thickness _ordinaryMenuMargin;
    private VerticalAlignment _ordinaryMenuVerticalAlignment;
    private BitmapImage? _compactTitleBarPackageIcon;

    public CompactMainWindow() : base(useStandardWindowAppearance: false)
    {
        _compactTitleBarConfig = Config;
        _ordinaryDockingMargin = DockingManager1.Margin;
        _ordinaryMenuMargin = Menu1.Margin;
        _ordinaryMenuVerticalAlignment = Menu1.VerticalAlignment;
        _compactTitleBarConfig.PropertyChanged += CompactTitleBarConfigChanged;
        SourceInitialized += AttachCompactTitleBar;
        MainWindowTitleBar.SizeChanged += CompactTitleBarSizeChanged;
        Closed += CloseCompactTitleBar;
        CompactActionsOverflowButton.Click += CompactActionsOverflowButton_Click;
        // Register our suspension before the shared full-screen helper changes native styles.
        this.SetWindowFull(Config);
    }

    private void AttachCompactTitleBar(object? sender, EventArgs e)
    {
        SourceInitialized -= AttachCompactTitleBar;
        var chrome = new CompactTitleBarChrome(this, MainWindowTitleBar, NativeCaptionButtonsPlaceholder, Root);
        try
        {
            if (!chrome.TryAttach())
            {
                chrome.Dispose();
                this.ApplyCaption();
                log.Info("Compact title bar is unavailable on this window; retaining the native title bar.");
                return;
            }

            _compactTitleBar = chrome;
            // The normal shell deliberately overlaps its menu row by 3 DIP. A glass caption
            // needs a strict content boundary so the workspace cannot paint over native buttons.
            DockingManager1.Margin = new Thickness(0);
            SetCompactMenuAlignment(true);
            CompactWindowIcon.Visibility = Visibility.Visible;
            CompactDragRegion.Visibility = Visibility.Visible;
            _compactTitleBarPackageIcon = ThemeManagerExtensions.TryLoadPackageIcon(this);
            _compactTitleBarThemeManager = ThemeManager.Current;
            _compactTitleBarThemeManager.CurrentUIThemeChanged += ApplyCompactTitleBarTheme;
            ApplyCompactTitleBarTheme(_compactTitleBarThemeManager.CurrentUITheme);
            var actionStyle = (Style)FindResource("CompactTitleBarActionButtonStyle");
            foreach (Button button in RightMenuItemPanel.Children.OfType<Button>())
                CompactTitleBarActions.ConfigureButton(button, actionStyle);
            Loaded += CompactTitleBarLoaded;
            log.Info("Experimental compact title bar attached with native caption buttons.");
        }
        catch (Exception ex)
        {
            if (_compactTitleBarThemeManager != null)
                _compactTitleBarThemeManager.CurrentUIThemeChanged -= ApplyCompactTitleBarTheme;
            _compactTitleBarThemeManager = null;
            chrome.Dispose();
            _compactTitleBar = null;
            DockingManager1.Margin = _ordinaryDockingMargin;
            SetCompactMenuAlignment(false);
            CompactWindowIcon.Visibility = Visibility.Collapsed;
            CompactDragRegion.Visibility = Visibility.Collapsed;
            CompactActionsOverflowButton.Visibility = Visibility.Collapsed;
            foreach (Button button in RightMenuItemPanel.Children.OfType<Button>())
            {
                button.ClearValue(StyleProperty);
                button.Width = 20;
                button.ClearValue(HeightProperty);
                button.Margin = new Thickness(0, 0, 5, 0);
                button.ClearValue(VerticalAlignmentProperty);
                button.ClearValue(WindowChrome.IsHitTestVisibleInChromeProperty);
            }
            this.ApplyCaption();
            log.Warn("Compact title bar initialization failed; retaining the native title bar.", ex);
        }
    }

    private void CompactTitleBarLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= CompactTitleBarLoaded;
        UpdateRightMenuVisibility();
    }

    private void SetCompactMenuAlignment(bool compact)
    {
        Menu1.SetCurrentValue(VerticalAlignmentProperty, compact ? VerticalAlignment.Center : _ordinaryMenuVerticalAlignment);
        // Centering a box with 4 DIP additional top margin lowers its visible center by 2 DIP.
        // Keep the workspace boundary unchanged instead of compensating with DockingManager.Margin.
        Menu1.SetCurrentValue(MarginProperty, compact
            ? new Thickness(_ordinaryMenuMargin.Left, _ordinaryMenuMargin.Top + 4, _ordinaryMenuMargin.Right, _ordinaryMenuMargin.Bottom)
            : _ordinaryMenuMargin);
    }

    private void ApplyCompactTitleBarTheme(Theme theme)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => ApplyCompactTitleBarTheme(theme));
            return;
        }
        if (_compactTitleBarClosed || _compactTitleBar == null)
            return;

        bool isDark = theme == Theme.Dark;
        _compactTitleBar.ApplyTheme(isDark);
        if (_compactTitleBarPackageIcon != null)
        {
            Icon = _compactTitleBarPackageIcon;
        }
        else
        {
            var icon = new BitmapImage(new Uri($"pack://application:,,,/ColorVision.Themes;component/Assets/Image/{(isDark ? "ColorVision1.ico" : "ColorVision.ico")}"));
            icon.Freeze();
            Icon = icon;
        }
    }

    private void CompactTitleBarConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowConfig.IsFull) || _compactTitleBar == null)
            return;

        if (_compactTitleBarConfig!.IsFull)
        {
            // Registered before SetWindowFull: remove chrome before that helper sets WindowStyle.None.
            _compactTitleBar.SetFullScreen(true);
            DockingManager1.Margin = _ordinaryDockingMargin;
            SetCompactMenuAlignment(false);
            CompactWindowIcon.Visibility = Visibility.Collapsed;
            CompactDragRegion.Visibility = Visibility.Collapsed;
            UpdateRightMenuVisibility();
        }
        else
        {
            // Resume after SetWindowFull has restored the original WindowStyle and WindowState.
            Dispatcher.BeginInvoke(() =>
            {
                if (!_compactTitleBarClosed && _compactTitleBarConfig?.IsFull == false)
                {
                    _compactTitleBar?.SetFullScreen(false);
                    DockingManager1.Margin = new Thickness(0);
                    SetCompactMenuAlignment(_compactTitleBar?.IsAttached == true);
                    CompactWindowIcon.Visibility = _compactTitleBar?.IsAttached == true ? Visibility.Visible : Visibility.Collapsed;
                    CompactDragRegion.Visibility = CompactWindowIcon.Visibility;
                    UpdateRightMenuVisibility();
                }
            }, DispatcherPriority.Loaded);
        }
    }

    private void CompactTitleBarSizeChanged(object sender, SizeChangedEventArgs e) => UpdateRightMenuVisibility();

    private void CompactActionsOverflowButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = CompactTitleBarActions.CreateMenu(RightMenuItemPanel.Children.OfType<Button>(),
            UpdateNotificationButton, CombinedUpdateCoordinator.HasPendingStartupUpdate);
        menu.PlacementTarget = CompactActionsOverflowButton;
        menu.Placement = PlacementMode.Bottom;
        CompactActionsOverflowButton.ContextMenu = menu;
        menu.IsOpen = true;
    }

    protected override void UpdateRightMenuVisibility()
    {
        if (_compactTitleBar?.IsAttached != true || _compactTitleBarConfig?.IsFull == true)
        {
            CompactActionsOverflowButton.Visibility = Visibility.Collapsed;
            CompactUpdateBadge.Visibility = Visibility.Collapsed;
            if (CompactActionsOverflowButton.ContextMenu is { } inactiveMenu)
                inactiveMenu.IsOpen = false;
            UpdateNotificationButton.Visibility = CombinedUpdateCoordinator.HasPendingStartupUpdate ? Visibility.Visible : Visibility.Collapsed;
            base.UpdateRightMenuVisibility();
            return;
        }

        CompactTitleBarLayout.Update(MainWindowTitleBar, Menu1, RightMenuItemPanel,
            UpdateNotificationButton, CompactWindowIcon, CompactDragRegion, CompactActionsOverflowButton,
            CombinedUpdateCoordinator.HasPendingStartupUpdate);
        bool hiddenUpdate = CombinedUpdateCoordinator.HasPendingStartupUpdate && UpdateNotificationButton.Visibility != Visibility.Visible;
        CompactUpdateBadge.Visibility = hiddenUpdate ? Visibility.Visible : Visibility.Collapsed;
        string overflowDescription = hiddenUpdate
            ? $"{Properties.Resources.CompactTitleBarMoreActions} · {UpdateNotificationButton.Content}"
            : Properties.Resources.CompactTitleBarMoreActions;
        CompactActionsOverflowButton.ToolTip = overflowDescription;
        AutomationProperties.SetName(CompactActionsOverflowButton, overflowDescription);
        if (CompactActionsOverflowButton.Visibility != Visibility.Visible && CompactActionsOverflowButton.ContextMenu is { } menu)
            menu.IsOpen = false;
    }

    private void CloseCompactTitleBar(object? sender, EventArgs e)
    {
        _compactTitleBarClosed = true;
        SourceInitialized -= AttachCompactTitleBar;
        Loaded -= CompactTitleBarLoaded;
        MainWindowTitleBar.SizeChanged -= CompactTitleBarSizeChanged;
        CompactActionsOverflowButton.Click -= CompactActionsOverflowButton_Click;
        if (CompactActionsOverflowButton.ContextMenu is { } menu)
            menu.IsOpen = false;
        CompactActionsOverflowButton.ContextMenu = null;
        if (_compactTitleBarConfig != null)
            _compactTitleBarConfig.PropertyChanged -= CompactTitleBarConfigChanged;
        if (_compactTitleBarThemeManager != null)
            _compactTitleBarThemeManager.CurrentUIThemeChanged -= ApplyCompactTitleBarTheme;
        _compactTitleBar?.Dispose();
        _compactTitleBar = null;
        _compactTitleBarConfig = null;
        _compactTitleBarThemeManager = null;
        _compactTitleBarPackageIcon = null;
        Closed -= CloseCompactTitleBar;
    }
}
