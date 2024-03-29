﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
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
            "/HandyControl;component/Themes/basic/colors/colorsviolet.xaml",
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
    }
}
