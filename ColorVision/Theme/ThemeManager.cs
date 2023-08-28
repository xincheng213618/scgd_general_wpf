using Microsoft.Win32;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Theme
{
    public enum Theme
    {
        [Description("浅色")]
        Light,
        [Description("深色")]
        Dark,
        [Description("跟随系统")]
        UseSystem
    };

    public static class ThemeManagerExtensions
    {
        public static void ApplyTheme(this Application app, Theme theme) => ThemeManager.Current.ApplyTheme(app, theme);
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
                if (ApplicationTheme == Theme.UseSystem)
                {
                    ApplyActTheme(Application.Current,e);
                }
            };
        }



        public void ApplyTheme(Application app, Theme theme) 
        {
            if (ApplicationTheme == theme)
                return;
            ApplicationTheme = theme;
            if (theme == Theme.UseSystem)
                theme = AppsTheme;
            ApplyActTheme(app,theme);
        }

        private void ApplyActTheme(Application app, Theme theme)
        {
            if (ApplicationActTheme == theme)
                return;
            ApplicationActTheme = theme;
            MessageBox.Show(theme.ToString());
        }



        /// <summary>
        /// 选择的主题，存在三种情况：
        /// </summary>
        public  Theme ApplicationTheme { get;  private set; }
        private Theme ApplicationActTheme { get;  set; }


        /// <summary>
        /// Windows,APP的主题
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


        public event ThemeChangedHandler? SystemThemeChanged;

        public event ThemeChangedHandler? AppsThemeChanged;
    }
}
