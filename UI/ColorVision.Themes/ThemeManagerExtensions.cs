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
            RoutedEventHandler routedEventHandler = null;
            ThemeChangedHandler themeChangedHandler = null;

            routedEventHandler = (s, e) =>
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                BitmapImage? packageIcon = TryLoadPackageIcon(window);
                if (packageIcon != null)
                    window.Icon = packageIcon;

                themeChangedHandler = theme =>
                {
                    if (Icon && packageIcon == null)
                        window.Icon = CreateDefaultIcon(theme);
                    ThemeManager.SetWindowTitleBarColor(hwnd, theme);
                };
                ThemeManager.Current.CurrentThemeChanged += themeChangedHandler;

                if (Icon && packageIcon == null)
                    window.Icon = CreateDefaultIcon(ThemeManager.Current.CurrentUITheme);

                ThemeManager.SetWindowTitleBarColor(hwnd, ThemeManager.Current.CurrentUITheme);

                window.Loaded -= routedEventHandler;
                window.Closed += (sender2, e2) =>
                {
                    if (themeChangedHandler != null)
                        ThemeManager.Current.CurrentThemeChanged -= themeChangedHandler;
                };
            };
            window.Loaded += routedEventHandler;
        }

        private static BitmapImage CreateDefaultIcon(Theme theme) => new(new Uri($"pack://application:,,,/ColorVision.Themes;component/Assets/Image/{(theme == Theme.Dark ? "ColorVision1.ico" : "ColorVision.ico")}"));

        private static BitmapImage? TryLoadPackageIcon(Window window)
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
