using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Theme
{
    public enum Theme
    {
        [Description("深色")]
        Dark,
        [Description("浅色")]
        Light,
    };

    public delegate void ThemeChangedHandler(Theme oldtheme,Theme newtheme);

    public static class ThemeManager
    {

        public static Application Application { get; private set; }

        public static void ApplyTheme(this Application app, Theme theme)
        {
            Application = app;
            CurrentUITheme = theme;


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
        }


        public static Theme CurrentUITheme { get; set; } = Theme.Dark;

        public static Theme AppsTheme { get => _AppsTheme; set { if (value == _AppsTheme) return; AppsThemeChanged?.Invoke(_AppsTheme, value); _AppsTheme = value; } }
        private static Theme _AppsTheme = AppsUseLightTheme() ? Theme.Light : Theme.Dark;


        public static Theme SystemTheme { get => _SystemTheme; set { if (value == _SystemTheme) return; SystemThemeChanged?.Invoke(_SystemTheme, value);  _SystemTheme = value; } }
        private static Theme _SystemTheme = SystemUsesLightTheme() ? Theme.Light : Theme.Dark;


        public static bool AppsUseLightTheme()
        {

            const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            const string RegistryValueName = "AppsUseLightTheme";
            
            // 这里也可能是LocalMachine(HKEY_LOCAL_MACHINE)
            // see "https://www.addictivetips.com/windows-tips/how-to-enable-the-dark-theme-in-windows-10/"
            object registryValueObject = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryKeyPath)?.GetValue(RegistryValueName);
            if (registryValueObject is null) return true;
            return (int)registryValueObject > 0;
        }

        public static bool SystemUsesLightTheme()
        {
            const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            const string RegistryValueName = "SystemUsesLightTheme";
            // 这里也可能是LocalMachine(HKEY_LOCAL_MACHINE)
            // see "https://www.addictivetips.com/windows-tips/how-to-enable-the-dark-theme-in-windows-10/"
            object registryValueObject = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryKeyPath)?.GetValue(RegistryValueName);
            if (registryValueObject is null) return true;
            return (int)registryValueObject > 0;
        }


        public static event ThemeChangedHandler? SystemThemeChanged;

        public static event ThemeChangedHandler? AppsThemeChanged;
    }
}
