using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ColorVision.Themes
{
    public static class ThemeManagerExtensions
    {
        private const string DefaultIconUri = "pack://application:,,,/ColorVision.Themes;component/Assets/Image/ColorVision1.ico";

        /// <summary>
        /// 更新主题
        /// </summary>
        public static void ApplyTheme(this Application app, Theme theme) => ThemeManager.Current.ApplyTheme(app, theme);
        /// <summary>
        /// 强制更新主题，即使主题一致也会更新
        /// </summary>
        public static void ForceApplyTheme(this Application app, Theme theme) => ThemeManager.Current.ApplyThemeChanged(app, theme);

        public static void ApplyCaption(this Window window, bool Icon = true)
        {
            window.Dispatcher.VerifyAccess();
            if (window.GetValue(CaptionSubscriptionProperty) == null)
                window.SetValue(CaptionSubscriptionProperty, new CaptionSubscription(window, Icon));
        }

        private static readonly DependencyProperty CaptionSubscriptionProperty = DependencyProperty.RegisterAttached(
            "CaptionSubscription", typeof(CaptionSubscription), typeof(ThemeManagerExtensions));

        private sealed class CaptionSubscription
        {
            private readonly Window window;
            private readonly bool useDefaultIcon;
            private ThemeManager? publisher;
            private IntPtr hwnd;
            private bool closed;

            internal CaptionSubscription(Window window, bool useDefaultIcon)
            {
                this.window = window;
                this.useDefaultIcon = useDefaultIcon;
                window.Closed += Closed;
                if (new WindowInteropHelper(window).Handle != IntPtr.Zero) Initialize();
                else window.SourceInitialized += SourceInitialized;
            }

            private void SourceInitialized(object? sender, EventArgs e) => Initialize();

            private void Initialize()
            {
                window.SourceInitialized -= SourceInitialized;
                hwnd = new WindowInteropHelper(window).Handle;
                BitmapSource? icon = TryLoadPackageIcon(window);
                if (icon == null && useDefaultIcon) icon = TryLoadDefaultWindowIcon();
                if (icon != null) window.Icon = icon;
                publisher = ThemeManager.Current;
                publisher.CurrentUIThemeChanged += Apply;
                Apply(publisher.CurrentUITheme);
            }

            private void Apply(Theme theme)
            {
                if (!window.Dispatcher.CheckAccess())
                {
                    if (!window.Dispatcher.HasShutdownStarted) window.Dispatcher.BeginInvoke(() => Apply(theme));
                    return;
                }
                if (closed) return;
                ThemeManager.SetWindowTitleBarColor(hwnd, theme);
            }

            private void Closed(object? sender, EventArgs e)
            {
                closed = true;
                window.SourceInitialized -= SourceInitialized;
                window.Closed -= Closed;
                if (publisher != null) publisher.CurrentUIThemeChanged -= Apply;
                publisher = null;
                window.ClearValue(CaptionSubscriptionProperty);
            }
        }

        /// <summary>Loads the default application icon for any theme.</summary>
        public static BitmapImage LoadDefaultIcon()
        {
            BitmapImage icon = new(new Uri(DefaultIconUri));
            if (icon.CanFreeze) icon.Freeze();
            return icon;
        }

        /// <summary>Keeps the ICO frames available so WPF can select each native window icon size.</summary>
        public static BitmapSource LoadDefaultWindowIcon() =>
            BitmapFrame.Create(new Uri(DefaultIconUri), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

        private static BitmapSource? TryLoadDefaultWindowIcon()
        {
            try
            {
                return LoadDefaultWindowIcon();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Loads and freezes a package-specific window icon without applying any native frame styling.</summary>
        public static BitmapImage? TryLoadPackageIcon(Window window)
        {
            try
            {
                string assemblyLocation = window.GetType().Assembly.Location;
                if (string.IsNullOrWhiteSpace(assemblyLocation))
                    return null;

                string? directory = Path.GetDirectoryName(assemblyLocation);
                if (string.IsNullOrWhiteSpace(directory))
                    return null;

                string iconPath = Path.Combine(directory, "PackageIcon.png");
                if (!File.Exists(iconPath))
                    return null;

                BitmapImage image = new();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(iconPath, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }
    }
}
