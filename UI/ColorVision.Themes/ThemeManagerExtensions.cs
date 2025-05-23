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
            routedEventHandler = (s, e) =>
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;

                if (Icon)
                {
                    ThemeManager.Current.CurrentThemeChanged += (theme) =>
                    {
                        window.Icon = new BitmapImage(new Uri($"pack://application:,,,/ColorVision.Themes;component/Assets/Image/{(theme == Theme.Dark ? "ColorVision1.ico" : "ColorVision.ico")}"));
                        ThemeManager.SetWindowTitleBarColor(hwnd, theme);
                    };
                    if (ThemeManager.Current.CurrentTheme == Theme.Dark)
                        window.Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision.Themes;component/Assets/Image/ColorVision1.ico"));
                }

                ThemeManager.SetWindowTitleBarColor(hwnd, ThemeManager.Current.CurrentUITheme);

                // 移除 Loaded 事件处理程序
                window.Loaded -= routedEventHandler;
            };
            window.Loaded += routedEventHandler;
        }
    }
}
