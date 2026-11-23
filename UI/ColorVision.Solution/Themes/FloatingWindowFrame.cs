using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ColorVision.Themes.Controls;
using Screen = System.Windows.Forms.Screen;

namespace ColorVision.Solution.Themes;

/// <summary>Arranges floating window chrome within the visible part of its maximized client area.</summary>
public sealed class FloatingWindowFrame : Decorator
{
    private static readonly DependencyPropertyKey DockHostWindowPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(DockHostWindow), typeof(Window), typeof(FloatingWindowFrame), new PropertyMetadata(null));
    public static readonly DependencyProperty DockHostWindowProperty = DockHostWindowPropertyKey.DependencyProperty;
    public Window? DockHostWindow => (Window?)GetValue(DockHostWindowProperty);

    public static readonly DependencyProperty IsCaptionProperty = DependencyProperty.RegisterAttached(
        "IsCaption", typeof(bool), typeof(FloatingWindowFrame), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetIsCaption(DependencyObject element) => (bool)element.GetValue(IsCaptionProperty);
    public static void SetIsCaption(DependencyObject element, bool value) => element.SetValue(IsCaptionProperty, value);

    private Window? window;
    private HwndSource? source;
    private DispatcherOperation? registerHook;

    public FloatingWindowFrame()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        window = Window.GetWindow(this);
        if (window == null) return;
        if (window is AvalonDock.Controls.LayoutDocumentFloatingWindowControl document && document.Model.Root?.Manager is { } manager)
            SetValue(DockHostWindowPropertyKey, Window.GetWindow(manager));
        source = PresentationSource.FromVisual(window) as HwndSource;
        // AvalonDock installs its nonclient handler while loading the window.
        // Register after that pass so our visible caption gets the first hit test.
        registerHook = Dispatcher.InvokeAsync(() =>
        {
            if (source == null) return;
            source.AddHook(HitTestMaximizedCaption);
            // The template paints the active/inactive outline. Keep the compositor's shadow.
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
                WindowAccentCompositor.SetWindowAttribute(source.Handle, WindowAccentCompositor.DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, -2); // DWMWA_COLOR_NONE
        }, DispatcherPriority.Loaded);
        window.LocationChanged += UpdateInsets;
        window.StateChanged += UpdateInsets;
        window.SizeChanged += UpdateInsets;
        window.DpiChanged += UpdateInsets;
        UpdateInsets(this, EventArgs.Empty);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (window == null) return;
        registerHook?.Abort();
        registerHook = null;
        source?.RemoveHook(HitTestMaximizedCaption);
        if (source is { IsDisposed: false } && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            WindowAccentCompositor.SetWindowAttribute(source.Handle, WindowAccentCompositor.DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, -1); // DWMWA_COLOR_DEFAULT
        source = null;
        window.LocationChanged -= UpdateInsets;
        window.StateChanged -= UpdateInsets;
        window.SizeChanged -= UpdateInsets;
        window.DpiChanged -= UpdateInsets;
        ClearValue(DockHostWindowPropertyKey);
        window = null;
    }

    private IntPtr HitTestMaximizedCaption(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmNcHitTest = 0x84;
        if (message != wmNcHitTest || window?.WindowState != WindowState.Maximized) return IntPtr.Zero;

        // WindowChrome measures its caption from the HWND edge, before our work-area inset.
        // Hit test the painted header so even its bottom edge drags and double-clicks to restore.
        long coordinates = lParam.ToInt64();
        var position = new Point(unchecked((short)coordinates), unchecked((short)(coordinates >> 16)));
        var hit = InputHitTest(PointFromScreen(position));
        if (hit is not DependencyObject element || !GetIsCaption(element)) return IntPtr.Zero;

        handled = true;
        bool isButton = System.Windows.Shell.WindowChrome.GetIsHitTestVisibleInChrome(hit) ||
            Microsoft.Windows.Shell.WindowChrome.GetIsHitTestVisibleInChrome(hit);
        return isButton ? 1 : 2; // HTCLIENT / HTCAPTION
    }

    private void UpdateInsets(object? sender, EventArgs e)
    {
        Thickness insets = default;
        if (window?.WindowState == WindowState.Maximized && source != null)
        {
            // Custom WindowChrome includes the invisible resize frame in its client area.
            // Compare screen pixels before converting the overhang to this monitor's WPF units.
            var workArea = Screen.FromHandle(source.Handle).WorkingArea;
            Point topLeft = window.PointToScreen(new Point());
            Point bottomRight = window.PointToScreen(new Point(window.RenderSize.Width, window.RenderSize.Height));
            DpiScale dpi = VisualTreeHelper.GetDpi(window);
            insets = new Thickness(
                Math.Max(0, workArea.Left - topLeft.X) / dpi.DpiScaleX,
                Math.Max(0, workArea.Top - topLeft.Y) / dpi.DpiScaleY,
                Math.Max(0, bottomRight.X - workArea.Right) / dpi.DpiScaleX,
                Math.Max(0, bottomRight.Y - workArea.Bottom) / dpi.DpiScaleY);
        }
        Margin = insets;
    }
}
