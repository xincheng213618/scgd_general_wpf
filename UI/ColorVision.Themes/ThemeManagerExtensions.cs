using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ColorVision.Themes
{
    public static class ThemeManagerExtensions
    {
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
            private BitmapImage? packageIcon;
            private IntPtr hwnd;
            private bool closed;

            internal CaptionSubscription(Window window, bool useDefaultIcon)
            {
                this.window = window;
                this.useDefaultIcon = useDefaultIcon;
                window.Closed += Closed;
                if (window.IsLoaded) Initialize();
                else window.Loaded += Loaded;
            }

            private void Loaded(object sender, RoutedEventArgs e) => Initialize();

            private void Initialize()
            {
                window.Loaded -= Loaded;
                hwnd = new WindowInteropHelper(window).Handle;
                packageIcon = TryLoadPackageIcon(window);
                if (packageIcon != null) window.Icon = packageIcon;
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
                if (useDefaultIcon && packageIcon == null) window.Icon = CreateDefaultIcon(theme);
                ThemeManager.SetWindowTitleBarColor(hwnd, theme);
            }

            private void Closed(object? sender, EventArgs e)
            {
                closed = true;
                window.Loaded -= Loaded;
                window.Closed -= Closed;
                if (publisher != null) publisher.CurrentUIThemeChanged -= Apply;
                publisher = null;
                window.ClearValue(CaptionSubscriptionProperty);
            }
        }

        private static BitmapImage CreateDefaultIcon(Theme theme) => new(new Uri($"pack://application:,,,/ColorVision.Themes;component/Assets/Image/{(theme == Theme.Dark ? "ColorVision1.ico" : "ColorVision.ico")}"));

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
