using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;

namespace ColorVision.Themes
{
    public enum Theme
    {
        [Description("ThemeUseSystem")]
        UseSystem,
        [Description("ThemeLight")]
        Light,
        [Description("ThemeDark")]
        Dark,
        [Description("ThemePink")]
        Pink
    };

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

        public static void ApplyCaption(this Window window)
        {
            RoutedEventHandler routedEventHandler = null;
            routedEventHandler = (s, e) =>
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;

                ThemeManager.Current.CurrentThemeChanged += (theme) =>
                {
                    window.Icon = new BitmapImage(new Uri($"pack://application:,,,/ColorVision.Util;component/Assets/Image/{(theme == Theme.Dark ? "ColorVision1.ico" : "ColorVision.ico")}"));
                    ThemeManager.SetWindowTitleBarColor(hwnd, theme);
                };
                if (ThemeManager.Current.CurrentTheme == Theme.Dark)
                    window.Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision.Util;component/Assets/Image/ColorVision1.ico"));

                ThemeManager.SetWindowTitleBarColor(hwnd, ThemeManager.Current.CurrentUITheme);

                // 移除 Loaded 事件处理程序
                window.Loaded -= routedEventHandler;
            };
            window.Loaded += routedEventHandler;
        }
    }

    public delegate void ThemeChangedHandler(Theme newtheme);

    public class ThemeManager
    {
        public Application Application { get; private set; }

        public static ThemeManager Current { get; set; } = new ThemeManager();

        public ThemeManager()
        {
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                AppsTheme = AppsUseLightTheme() ? Theme.Light : Theme.Dark;
                SystemTheme = SystemUsesLightTheme() ? Theme.Light : Theme.Dark;
            };
            SystemParameters.StaticPropertyChanged += (s, e) =>
            {
                AppsTheme = AppsUseLightTheme() ? Theme.Light : Theme.Dark;
                SystemTheme = SystemUsesLightTheme() ? Theme.Light : Theme.Dark;
            };

            AppsThemeChanged += (e) =>
            {
                if (CurrentTheme == Theme.UseSystem)
                {
                    ApplyActTheme(Application.Current,e);
                }
            };
        }   

        public void ApplyTheme(Application app, Theme theme) 
        {
            if (CurrentTheme == theme)
                return;
            CurrentTheme = theme;
            if (theme == Theme.UseSystem)
                theme = AppsTheme;
            ApplyActTheme(app,theme);
        }

        public static List<string> ResourceDictionaryDark { get; set; } = new List<string>()
        {
            "/HandyControl;component/themes/basic/colors/colorsdark.xaml",
            "/HandyControl;component/Themes/Theme.xaml",
            "/ColorVision.Util;component/Themes/Dark.xaml",
            "/ColorVision.Util;component/Themes/Base.xaml",
            "/ColorVision.Util;component/Themes/Menu.xaml",
            "/ColorVision.Util;component/Themes/GroupBox.xaml" ,
            "/ColorVision.Util;component/Themes/Icons.xaml",
            "/ColorVision.Util;component/Themes/Window/BaseWindow.xaml"
        };

        public static List<string> ResourceDictionaryWhite { get; set; } = new List<string>()
        {
            "/HandyControl;component/Themes/basic/colors/colors.xaml",
            "/HandyControl;component/Themes/Theme.xaml",
            "/ColorVision.Util;component/Themes/White.xaml",
            "/ColorVision.Util;component/Themes/Base.xaml",
            "/ColorVision.Util;component/Themes/Menu.xaml",
            "/ColorVision.Util;component/Themes/GroupBox.xaml" ,
            "/ColorVision.Util;component/Themes/Icons.xaml",
            "/ColorVision.Util;component/Themes/Window/BaseWindow.xaml"
        };

        public static List<string> ResourceDictionaryPink { get; set; } = new List<string>()
        {
            "/ColorVision.Util;component/Themes/HPink.xaml",
            "/HandyControl;component/Themes/Theme.xaml",
            "/ColorVision.Util;component/Themes/White.xaml",
            "/ColorVision.Util;component/Themes/Pink.xaml",
            "/ColorVision.Util;component/Themes/Base.xaml",
            "/ColorVision.Util;component/Themes/Menu.xaml",
            "/ColorVision.Util;component/Themes/GroupBox.xaml" ,
            "/ColorVision.Util;component/Themes/Icons.xaml",
            "/ColorVision.Util;component/Themes/Window/BaseWindow.xaml"
        };

        private void ApplyActTheme(Application app, Theme theme)
        {
            if (CurrentUITheme == theme) return;
            ApplyThemeChanged(app, theme);
        }

        public void ApplyThemeChanged(Application app, Theme theme)
        {
            switch (theme)
            {
                case Theme.Light:
                    var light = new Wpf.Ui.Markup.ThemesDictionary();
                    light.Theme = ThemeType.Light;
                    app.Resources.MergedDictionaries.Add(light);
                    app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());

                    foreach (var item in ResourceDictionaryWhite)
                    {
                        ResourceDictionary dictionary = Application.LoadComponent(new Uri(item, UriKind.Relative)) as ResourceDictionary;
                        app.Resources.MergedDictionaries.Add(dictionary);
                    }
                    break;
                case Theme.Dark:
                    var dark = new Wpf.Ui.Markup.ThemesDictionary();
                    dark.Theme = ThemeType.Dark;
                    app.Resources.MergedDictionaries.Add(dark);
                    app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());

                    foreach (var item in ResourceDictionaryDark)
                    {
                        ResourceDictionary dictionary = Application.LoadComponent(new Uri(item, UriKind.Relative)) as ResourceDictionary;
                        app.Resources.MergedDictionaries.Add(dictionary);
                    }
                    break;
                case Theme.Pink:
                    var pink1 = new Wpf.Ui.Markup.ThemesDictionary();
                    pink1.Theme = ThemeType.Light;
                    app.Resources.MergedDictionaries.Add(pink1);
                    app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());

                    foreach (var item in ResourceDictionaryPink)
                    {
                        ResourceDictionary dictionary = Application.LoadComponent(new Uri(item, UriKind.Relative)) as ResourceDictionary;
                        app.Resources.MergedDictionaries.Add(dictionary);
                    }
                    break;
                case Theme.UseSystem:
                    break;
                default:
                    break;
            }
            CurrentUITheme = theme;
        }



        /// <summary>
        /// 选择的主题，存在三种情况：
        /// </summary>
        public  Theme? CurrentTheme { get => _CurrentTheme;  private set { if (value == _CurrentTheme) return; _CurrentTheme = value; 
                if (_CurrentTheme != null)
                    CurrentThemeChanged?.Invoke((Theme)_CurrentTheme); 
            } }
        private Theme? _CurrentTheme;

        //这里是两种
        public Theme CurrentUITheme { get => _CurrentUITheme; private set { if (value == _CurrentUITheme) return; _CurrentUITheme = value; CurrentUIThemeChanged?.Invoke(value);  } }
        private Theme _CurrentUITheme;


        public event ThemeChangedHandler? CurrentThemeChanged;
        public event ThemeChangedHandler? CurrentUIThemeChanged;


        /// <summary>
        /// Windows应用的主题
        /// </summary>
        public Theme AppsTheme { get => _AppsTheme; set { if (value == _AppsTheme) return;  AppsThemeChanged?.Invoke(value); _AppsTheme = value; } }
        private  Theme _AppsTheme = AppsUseLightTheme() ? Theme.Light : Theme.Dark;

        /// <summary>
        /// 任务栏的主题，这里Win10和Win11的表现不一样
        /// </summary>
        public  Theme SystemTheme { get => _SystemTheme; set { if (value == _SystemTheme) return; SystemThemeChanged?.Invoke(value);  _SystemTheme = value; } }
        private  Theme _SystemTheme = SystemUsesLightTheme() ? Theme.Light : Theme.Dark;


        public static bool AppsUseLightTheme()
        {

            const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            const string RegistryValueName = "AppsUseLightTheme";
            
            // 这里也可能是LocalMachine(HKEY_LOCAL_MACHINE)
            // see "https://www.addictivetips.com/windows-tips/how-to-enable-the-dark-theme-in-windows-10/"
            object registryValueObject = Registry.CurrentUser.OpenSubKey(RegistryKeyPath)?.GetValue(RegistryValueName);
            if (registryValueObject is null) return true;
            return (int)registryValueObject > 0;
        }

        public static bool SystemUsesLightTheme()
        {
            const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            const string RegistryValueName = "SystemUsesLightTheme";
            // 这里也可能是LocalMachine(HKEY_LOCAL_MACHINE)
            // see "https://www.addictivetips.com/windows-tips/how-to-enable-the-dark-theme-in-windows-10/"
            object registryValueObject = Registry.CurrentUser.OpenSubKey(RegistryKeyPath)?.GetValue(RegistryValueName);
            if (registryValueObject is null) return true;
            return (int)registryValueObject > 0;
        }


        public event ThemeChangedHandler? SystemThemeChanged;

        public event ThemeChangedHandler? AppsThemeChanged;

        public static void SetWindowTitleBarColor(IntPtr hwnd, Theme theme)
        {
            uint attribute;
            uint attributeSize = (uint)Marshal.SizeOf(typeof(uint));

            switch (theme)
            {
                case Theme.Dark:
                    // Reset caption color to system default
                    ResetCaptionColor(hwnd);

                    // Enable dark mode
                    attribute = 1;
                    _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref attribute, attributeSize);
                    _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref attribute, attributeSize);
                    break;

                case Theme.Pink:
                    // Disable dark mode
                    attribute = 0;
                    _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref attribute, attributeSize);
                    _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref attribute, attributeSize);

                    // Set caption color to pink
                    ///颜色值的字节顺序被调整了
                    // 原始颜色值: #E8A6C1
                    // 1. 分解为 RGB 分量:
                    //    红色 (Red) = E8
                    //    绿色 (Green) = A6
                    //    蓝色 (Blue) = C1
                    // 2. 调整字节顺序:
                    //    蓝色 (Blue) = C1
                    //    绿色 (Green) = A6
                    //    红色 (Red) = E8
                    // 3. 重新组合为新的十六进制表示:
                    //    新的颜色值 = 0xFFC1A6E8

                    // 设置标题栏颜色为 #E8A6C1
                    attribute = 0xC1A6E8;
                    _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR, ref attribute, attributeSize);
                    _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, ref attribute, attributeSize);
                    break;

                case Theme.Light:
                case Theme.UseSystem:
                default:
                    // Reset caption color to system default
                    ResetCaptionColor(hwnd);

                    // Disable dark mode
                    attribute = 0;
                    _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref attribute, attributeSize);
                    _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref attribute, attributeSize);
                    break;
            }
        }

        private static void ResetCaptionColor(IntPtr hwnd)
        {
            uint attribute = 0xFFFFFFFF; // White color
            uint attributeSize = (uint)Marshal.SizeOf(typeof(uint));
            _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR, ref attribute, attributeSize);
            _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, ref attribute, attributeSize);
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref uint pvAttribute, uint cbAttribute);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, IntPtr pvAttribute, uint cbAttribute);


#pragma warning disable CA1707
        [Flags]
        public enum DWMWINDOWATTRIBUTE : uint
        {
            //沉浸式暗模式20H1
            DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19,
            //沉浸式暗模式
            DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
            ///Might require Windows SDK 10.0.22000.0 (aka first Windows 11 SDK)
            //设置窗口边框颜色
            DWMWA_BORDER_COLOR = 34,
            //设置窗口标题栏颜色。
            DWMWA_CAPTION_COLOR = 35,
            //设置窗口标题栏文本颜色。
            DWMWA_TEXT_COLOR = 36,
        }
#pragma warning restore CA1707

    }
}
