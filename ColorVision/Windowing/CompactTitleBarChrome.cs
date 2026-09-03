using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace ColorVision.Windowing
{
    /// <summary>
    /// An opt-in, non-layered title-bar experiment. The application owns the left
    /// title-bar content; DWM continues to draw and hit-test the caption buttons.
    /// The root and caption placeholder must not paint over the native button area.
    /// </summary>
    public sealed class CompactTitleBarChrome : IDisposable
    {
        private readonly Window window;
        private readonly FrameworkElement titleBar;
        private readonly FrameworkElement captionButtonsPlaceholder;
        private readonly FrameworkElement contentRoot;
        private readonly WindowChrome chrome = new WindowChrome
        {
            UseAeroCaptionButtons = true,
            CornerRadius = new CornerRadius(0),
            GlassFrameThickness = new Thickness(0, 32, 0, 0),
            CaptionHeight = 32
        };

        private HwndSource? source;
        private CompactTitleBarVisibilityGuard? visibilityGuard;
        private IntPtr handle;
        private DispatcherOperation? pendingRefresh;
        private object? originalBackgroundResourceKey;
        private double originalTitleBarMinHeight;
        private double originalPlaceholderWidth;
        private Thickness originalRootMargin;
        private bool isFullScreen;
        private bool isDark;
        private bool isDisposed;
        private bool isClosed;
        private bool updatingMetrics;
        private bool refreshNativeTheme;
        private double dpiScale = 1;
        private double clientContentTop;
        private NativeRect nativeCaptionBounds;

        public CompactTitleBarChrome(Window window, FrameworkElement titleBar, FrameworkElement captionButtonsPlaceholder, FrameworkElement contentRoot)
        {
            this.window = window ?? throw new ArgumentNullException(nameof(window));
            this.titleBar = titleBar ?? throw new ArgumentNullException(nameof(titleBar));
            this.captionButtonsPlaceholder = captionButtonsPlaceholder ?? throw new ArgumentNullException(nameof(captionButtonsPlaceholder));
            this.contentRoot = contentRoot ?? throw new ArgumentNullException(nameof(contentRoot));
        }

        /// <summary>True while the controller is attached, including a full-screen suspension.</summary>
        public bool IsAttached { get; private set; }

        /// <summary>
        /// Attach after SourceInitialized. Older systems retain the ordinary window:
        /// this experiment requires Windows 11's documented caption-color attributes.
        /// </summary>
        public bool TryAttach()
        {
            window.VerifyAccess();
            if (isDisposed)
                return false;
            if (IsAttached)
                return true;
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) || window.AllowsTransparency ||
                window.WindowStyle != WindowStyle.SingleBorderWindow || WindowChrome.GetWindowChrome(window) != null)
                return false;

            handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero || DwmIsCompositionEnabled(out bool compositionEnabled) != 0 || !compositionEnabled)
                return false;
            source = HwndSource.FromHwnd(handle);
            if (source == null || source.IsDisposed)
                return false;

            originalTitleBarMinHeight = titleBar.MinHeight;
            originalPlaceholderWidth = captionButtonsPlaceholder.Width;
            originalRootMargin = contentRoot.Margin;
            object originalBackgroundValue = window.ReadLocalValue(Window.BackgroundProperty);
            if (originalBackgroundValue != DependencyProperty.UnsetValue && originalBackgroundValue != null)
            {
                // WPF's public serializer converter exposes a DynamicResource as
                // its public markup extension; do not inspect internal expression fields.
                TypeConverter converter = TypeDescriptor.GetConverter(originalBackgroundValue);
                if (converter.CanConvertTo(typeof(MarkupExtension)) &&
                    converter.ConvertTo(originalBackgroundValue, typeof(MarkupExtension)) is DynamicResourceExtension resource)
                    originalBackgroundResourceKey = resource.ResourceKey;
            }
            IsAttached = true;
            window.Loaded += OnLoaded;
            window.StateChanged += OnStateChanged;
            window.Closed += OnClosed;
            titleBar.SizeChanged += OnTitleBarSizeChanged;

            UpdateMetrics();
            window.SetCurrentValue(Window.BackgroundProperty, Brushes.Transparent);
            WindowChrome.SetWindowChrome(window, chrome);
            // HwndSource invokes the newest hook first. Only the small client-band
            // correction below precedes WindowChrome's native hit testing.
            source.AddHook(WindowProc);
            visibilityGuard = new CompactTitleBarVisibilityGuard(window, handle, chrome);
            if (!visibilityGuard.TryAttach())
            {
                Dispose();
                return false;
            }
            ApplyNativeTheme();
            QueueRefresh();
            return true;
        }

        public void ApplyTheme(bool dark)
        {
            window.VerifyAccess();
            isDark = dark;
            if (IsAttached && !isFullScreen)
            {
                // SetCurrentValue retains the existing DynamicResource/Binding expression.
                // A theme resource refresh can otherwise paint over the native buttons.
                window.SetCurrentValue(Window.BackgroundProperty, Brushes.Transparent);
                ApplyNativeTheme();
            }
        }

        /// <summary>
        /// Suspend before the host changes WindowStyle, and resume after it restores
        /// the ordinary style/state. Full-screen policy remains with the host.
        /// </summary>
        public void SetFullScreen(bool fullScreen)
        {
            window.VerifyAccess();
            if (!IsAttached || isDisposed || isFullScreen == fullScreen)
                return;
            isFullScreen = fullScreen;
            if (fullScreen)
            {
                visibilityGuard?.SetEnabled(false);
                pendingRefresh?.Abort();
                pendingRefresh = null;
                WindowChrome.SetWindowChrome(window, null);
                RestoreBackground();
                contentRoot.SetCurrentValue(FrameworkElement.MarginProperty, originalRootMargin);
                titleBar.SetCurrentValue(FrameworkElement.MinHeightProperty, originalTitleBarMinHeight);
                captionButtonsPlaceholder.SetCurrentValue(FrameworkElement.WidthProperty, originalPlaceholderWidth);
            }
            else
            {
                window.SetCurrentValue(Window.BackgroundProperty, Brushes.Transparent);
                UpdateMetrics();
                WindowChrome.SetWindowChrome(window, chrome);
                source?.RemoveHook(WindowProc);
                source?.AddHook(WindowProc);
                visibilityGuard?.SetEnabled(true);
                ApplyNativeTheme();
                QueueRefresh();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => QueueRefresh();
        private void OnStateChanged(object? sender, EventArgs e)
        {
            // WM_SIZE raises StateChanged before HwndSource lays out the new client
            // size. Apply the inset now, avoiding a second layout at Loaded priority.
            if (IsAttached && !isFullScreen && !isDisposed && window.WindowState != WindowState.Minimized)
            {
                UpdateMetrics();
                if (refreshNativeTheme)
                    ApplyNativeTheme();
            }
        }

        private void OnTitleBarSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Width changes do not change the system caption metrics. In particular,
            // moving/resizing must not allocate a new chrome or rebuild the template.
            if (e.HeightChanged && !updatingMetrics)
                QueueRefresh();
        }

        private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Keep the caption envelope stable across maximize/restore. Its extra
            // bottom strip in a normal window is client content, not draggable chrome.
            // Leave resize edges, native buttons and all actual caption input to WPF.
            if (message == 0x0084 && IsClientContentBand(lParam)) // WM_NCHITTEST
            {
                handled = true;
                return new IntPtr(1); // HTCLIENT
            }
            if (message is 0x02E0 or 0x001A or 0x031A or 0x031E) // DPI / settings / theme / DWM
                QueueRefresh(true);
            return IntPtr.Zero;
        }

        private bool IsClientContentBand(IntPtr screenPosition)
        {
            if (!IsAttached || isFullScreen || isDisposed || window.WindowState != WindowState.Normal)
                return false;
            double captionBottom = chrome.ResizeBorderThickness.Top + chrome.CaptionHeight;
            if (clientContentTop >= captionBottom || !GetWindowRect(handle, out NativeRect bounds))
                return false;
            long coordinates = screenPosition.ToInt64();
            double pixelX = (short)(coordinates & 0xFFFF) - bounds.Left;
            double pixelY = (short)((coordinates >> 16) & 0xFFFF) - bounds.Top;
            if (pixelX >= nativeCaptionBounds.Left && pixelX < nativeCaptionBounds.Right &&
                pixelY >= nativeCaptionBounds.Top && pixelY < nativeCaptionBounds.Bottom)
                return false;
            double x = pixelX / dpiScale;
            double y = pixelY / dpiScale;
            return y >= clientContentTop && y < captionBottom &&
                x >= chrome.ResizeBorderThickness.Left &&
                x < (bounds.Right - bounds.Left) / dpiScale - chrome.ResizeBorderThickness.Right;
        }

        private void QueueRefresh(bool updateTheme = false)
        {
            if (!IsAttached || isFullScreen || isDisposed)
                return;
            refreshNativeTheme |= updateTheme;
            if (pendingRefresh?.Status == DispatcherOperationStatus.Pending)
                return;
            pendingRefresh = window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                pendingRefresh = null;
                if (!IsAttached || isFullScreen || isDisposed || window.WindowState == WindowState.Minimized)
                    return;
                UpdateMetrics();
                if (refreshNativeTheme)
                {
                    refreshNativeTheme = false;
                    ApplyNativeTheme();
                }
            }));
        }

        private void UpdateMetrics()
        {
            if (updatingMetrics || !IsAttached || isFullScreen)
                return;
            updatingMetrics = true;
            try
            {
                uint dpi = GetDpiForWindow(handle);
                if (dpi == 0)
                    dpi = 96;
                double scale = dpi / 96.0;
                dpiScale = scale;
                double frameX = (GetSystemMetricsForDpi(32, dpi) + GetSystemMetricsForDpi(92, dpi)) / scale;
                double frameY = (GetSystemMetricsForDpi(33, dpi) + GetSystemMetricsForDpi(92, dpi)) / scale;
                double nativeCaptionHeight = GetSystemMetricsForDpi(4, dpi) / scale;
                bool maximized = window.WindowState == WindowState.Maximized;
                double border = 1 / scale;
                var insets = maximized ? new Thickness(frameX, frameY, frameX, frameY) : new Thickness(border);
                var margin = new Thickness(originalRootMargin.Left + insets.Left, originalRootMargin.Top + insets.Top,
                    originalRootMargin.Right + insets.Right, originalRootMargin.Bottom + insets.Bottom);
                contentRoot.SetCurrentValue(FrameworkElement.MarginProperty, margin);

                // The title row has one height in both states. DWM button bounds
                // are relative to the HWND, so do not subtract a state-dependent inset.
                double captionHeight = Math.Max(32, nativeCaptionHeight + frameY);
                // DWM does not define caption bounds for invisible/minimized windows.
                // Reserve a conservative metric-derived width until Loaded can measure.
                double buttonsWidth = 3 * Math.Max(GetSystemMetricsForDpi(30, dpi) / scale, nativeCaptionHeight * 2);
                if (IsWindowVisible(handle) && window.WindowState != WindowState.Minimized &&
                    DwmGetWindowAttribute(handle, 5, out NativeRect captionBounds, Marshal.SizeOf<NativeRect>()) == 0 &&
                    captionBounds.Right > captionBounds.Left && captionBounds.Bottom > captionBounds.Top &&
                    GetWindowRect(handle, out NativeRect windowBounds))
                {
                    nativeCaptionBounds = captionBounds;
                    double measuredWidth = (windowBounds.Right - windowBounds.Left - captionBounds.Left) / scale - margin.Right;
                    if (measuredWidth > 0 && measuredWidth < window.ActualWidth)
                        buttonsWidth = measuredWidth;
                    captionHeight = Math.Max(captionHeight, captionBounds.Bottom / scale);
                }

                titleBar.SetCurrentValue(FrameworkElement.MinHeightProperty, Math.Max(originalTitleBarMinHeight, captionHeight));
                captionButtonsPlaceholder.SetCurrentValue(FrameworkElement.WidthProperty, Math.Ceiling(buttonsWidth * scale) / scale);
                double titleHeight = Math.Max(titleBar.MinHeight, titleBar.ActualHeight);
                clientContentTop = titleHeight + margin.Top;
                var resizeBorder = new Thickness(frameX, frameY, frameX, frameY);
                // Both GlassFrameThickness and CaptionHeight trigger WindowChrome's
                // full frame refresh (including SWP_FRAMECHANGED). Use the maximized
                // envelope in both states; only the client inset needs to change.
                var glassFrame = new Thickness(0, titleHeight + originalRootMargin.Top + frameY, 0, 0);
                if (chrome.ResizeBorderThickness != resizeBorder)
                    chrome.ResizeBorderThickness = resizeBorder;
                if (chrome.GlassFrameThickness != glassFrame)
                    chrome.GlassFrameThickness = glassFrame;
                // CaptionHeight begins below ResizeBorderThickness.Top in WindowChrome.
                double captionHitHeight = Math.Max(0, titleHeight + originalRootMargin.Top);
                if (chrome.CaptionHeight != captionHitHeight)
                    chrome.CaptionHeight = captionHitHeight;
            }
            finally
            {
                updatingMetrics = false;
            }
        }

        private void ApplyNativeTheme()
        {
            if (handle == IntPtr.Zero || isClosed)
                return;
            refreshNativeTheme = false;
            uint dark = isDark ? 1u : 0u;
            uint defaultColor = 0xFFFFFFFF;
            uint textColor = isDark ? 0x00FFFFFFu : 0x00000000u;
            var background = window.TryFindResource("GlobalBackground") as SolidColorBrush;
            Color color = background?.Color ?? (isDark ? Color.FromRgb(38, 38, 38) : Colors.White);
            uint captionColor = (uint)(color.R | color.G << 8 | color.B << 16);
            _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(uint));
            _ = DwmSetWindowAttribute(handle, 34, ref defaultColor, sizeof(uint));
            _ = DwmSetWindowAttribute(handle, 35, ref captionColor, sizeof(uint));
            _ = DwmSetWindowAttribute(handle, 36, ref textColor, sizeof(uint));
        }

        private void RestoreBackground()
        {
            // Recreate the same resource reference to discard WPF's cached resource
            // value even before Loaded. InvalidateProperty alone clears the current
            // override, but can return an old expression cache on a not-yet-shown HWND.
            if (originalBackgroundResourceKey != null)
                window.SetResourceReference(Window.BackgroundProperty, originalBackgroundResourceKey);
            else
                window.InvalidateProperty(Window.BackgroundProperty);
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            isClosed = true;
            Dispose();
        }

        public void Dispose()
        {
            window.VerifyAccess();
            if (isDisposed)
                return;
            isDisposed = true;
            visibilityGuard?.Dispose();
            visibilityGuard = null;
            pendingRefresh?.Abort();
            pendingRefresh = null;
            if (!IsAttached)
                return;
            IsAttached = false;
            window.Loaded -= OnLoaded;
            window.StateChanged -= OnStateChanged;
            window.Closed -= OnClosed;
            titleBar.SizeChanged -= OnTitleBarSizeChanged;
            if (source != null && !source.IsDisposed)
                source.RemoveHook(WindowProc);
            source = null;
            if (!isClosed)
            {
                WindowChrome.SetWindowChrome(window, null);
                RestoreBackground();
                titleBar.SetCurrentValue(FrameworkElement.MinHeightProperty, originalTitleBarMinHeight);
                captionButtonsPlaceholder.SetCurrentValue(FrameworkElement.WidthProperty, originalPlaceholderWidth);
                contentRoot.SetCurrentValue(FrameworkElement.MarginProperty, originalRootMargin);
                uint defaultColor = 0xFFFFFFFF;
                _ = DwmSetWindowAttribute(handle, 34, ref defaultColor, sizeof(uint));
                _ = DwmSetWindowAttribute(handle, 35, ref defaultColor, sizeof(uint));
                _ = DwmSetWindowAttribute(handle, 36, ref defaultColor, sizeof(uint));
            }
            handle = IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out NativeRect value, int size);
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint value, int size);
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern int GetSystemMetricsForDpi(int index, uint dpi);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rectangle);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hwnd);
    }
}
