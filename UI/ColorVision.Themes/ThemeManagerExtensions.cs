using System;
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

        public static void ApplyCaption(this Window window,bool Icon = true)   
        {
            RoutedEventHandler routedEventHandler = null;
            ThemeChangedHandler themeChangedHandler = null;

            routedEventHandler = (s, e) =>
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;

                if (Icon)
                {
                    themeChangedHandler = (theme) =>
                    {
                        window.Icon = new BitmapImage(new Uri($"pack://application:,,,/ColorVision.Themes;component/Assets/Image/{(theme == Theme.Dark ? "ColorVision1.ico" : "ColorVision.ico")}"));
                        ThemeManager.SetWindowTitleBarColor(hwnd, theme);
                    };
                    ThemeManager.Current.CurrentThemeChanged += themeChangedHandler;

                    if (ThemeManager.Current.CurrentTheme == Theme.Dark)
                        window.Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision.Themes;component/Assets/Image/ColorVision1.ico"));
                }

                ThemeManager.SetWindowTitleBarColor(hwnd, ThemeManager.Current.CurrentUITheme);

                // 解绑 Loaded 事件
                window.Loaded -= routedEventHandler;
                // 监听 Closed，窗口关闭时解绑主题事件
                window.Closed += (sender2, e2) =>
                {
                    if (themeChangedHandler != null)
                        ThemeManager.Current.CurrentThemeChanged -= themeChangedHandler;
                };
            };
            window.Loaded += routedEventHandler;

        }
    }
}
