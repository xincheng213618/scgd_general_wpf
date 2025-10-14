using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Appearance;

namespace ColorVision.Themes
{

    public delegate void ThemeChangedHandler(Theme newtheme);

    public class ThemeManager
    {
        public static ThemeManager Current { get; set; } = new ThemeManager();

        // 资源缓存，使用弱引用避免内存泄漏
        private readonly Dictionary<string, WeakReference<ResourceDictionary>> _resourceCache = new Dictionary<string, WeakReference<ResourceDictionary>>();
        private readonly object _cacheLock = new object();

        public ThemeManager()
        {
            DelayedInitialize();
            AppsThemeChanged += (e) =>
            {
                if (CurrentTheme == Theme.UseSystem)
                {
                    ApplyActTheme(Application.Current,e);
                }
            };
        }
        /// <summary>
        /// 这里加载需要500ms，放在启动时太浪费时间，所以延迟加载
        /// </summary>
        private async void DelayedInitialize()
        {
            // 延迟 1 秒（根据需要调整时间）
            await Task.Delay(10000);
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



        public void ApplyTheme(Application app, Theme theme) 
        {
            if (CurrentTheme == theme)
                return;
            CurrentTheme = theme;
            if (theme == Theme.UseSystem)
                theme = AppsTheme;
            ApplyActTheme(app,theme);
        }

        public static List<string> ResourceDictionaryBase { get; set; } = new List<string>()
        {
            "/ColorVision.Themes;component/Themes/Base.xaml",
            "/ColorVision.Themes;component/Themes/Menu.xaml",
            "/ColorVision.Themes;component/Themes/GroupBox.xaml" ,
            "/ColorVision.Themes;component/Themes/Icons.xaml",
            "/ColorVision.Themes;component/Themes/Window/BaseWindow.xaml"
        };

        public static List<string> ResourceDictionaryDark { get; set; } = new List<string>()
        {
            "/HandyControl;component/themes/basic/colors/colorsdark.xaml",
            "/HandyControl;component/Themes/Theme.xaml",
            "/ColorVision.Themes;component/Themes/Dark.xaml",
        };

        public static List<string> ResourceDictionaryWhite { get; set; } = new List<string>()
        {
            "/HandyControl;component/Themes/basic/colors/colors.xaml",
            "/HandyControl;component/Themes/Theme.xaml",
            "/ColorVision.Themes;component/Themes/White.xaml",
        };

        public static List<string> ResourceDictionaryPink { get; set; } = new List<string>()
        {
            "/ColorVision.Themes;component/Themes/HPink.xaml",
            "/HandyControl;component/Themes/Theme.xaml",
            "/ColorVision.Themes;component/Themes/White.xaml",
            "/ColorVision.Themes;component/Themes/Pink.xaml",
        };
        public static List<string> ResourceDictionaryCyan { get; set; } = new List<string>()
        {
            "/ColorVision.Themes;component/Themes/HCyan.xaml",
            "/HandyControl;component/Themes/Theme.xaml",
            "/ColorVision.Themes;component/Themes/White.xaml",
            "/ColorVision.Themes;component/Themes/Cyan.xaml",
        };

        private void ApplyActTheme(Application app, Theme theme)
        {
            if (CurrentUITheme == theme) return;
            ApplyThemeChanged(app, theme);
        }

        /// <summary>
        /// 从缓存加载资源字典，如果缓存中不存在则加载并缓存
        /// </summary>
        private ResourceDictionary? LoadResourceWithCache(string uri)
        {
            lock (_cacheLock)
            {
                // 尝试从缓存获取
                if (_resourceCache.TryGetValue(uri, out var weakRef))
                {
                    if (weakRef.TryGetTarget(out var cachedResource))
                    {
                        return cachedResource;
                    }
                    // 弱引用已被回收，从缓存中移除
                    _resourceCache.Remove(uri);
                }

                // 加载资源并缓存
                var resource = Application.LoadComponent(new Uri(uri, UriKind.Relative)) as ResourceDictionary;
                if (resource != null)
                {
                    _resourceCache[uri] = new WeakReference<ResourceDictionary>(resource);
                }
                return resource;
            }
        }

        /// <summary>
        /// 加载主题资源列表，避免重复加载
        /// </summary>
        private void LoadThemeResources(Application app, List<string> resources)
        {
            foreach (var item in resources)
            {
                var dictionary = LoadResourceWithCache(item);
                if (dictionary != null && !app.Resources.MergedDictionaries.Contains(dictionary))
                {
                    app.Resources.MergedDictionaries.Add(dictionary);
                }
            }
        }

        public void ApplyThemeChanged(Application app, Theme theme)
        {
            switch (theme)
            {
                case Theme.Light:
                    var light = new Wpf.Ui.Markup.ThemesDictionary();
                    light.Theme = ApplicationTheme.Light;
                    app.Resources.MergedDictionaries.Add(light);
                    app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());

                    LoadThemeResources(app, ResourceDictionaryWhite);
                    LoadThemeResources(app, ResourceDictionaryBase);
                    break;
                case Theme.Dark:
                    var dark = new Wpf.Ui.Markup.ThemesDictionary();
                    dark.Theme = ApplicationTheme.Dark;
                    app.Resources.MergedDictionaries.Add(dark);
                    app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());

                    LoadThemeResources(app, ResourceDictionaryDark);
                    LoadThemeResources(app, ResourceDictionaryBase);
                    break;
                case Theme.Pink:
                    var pink1 = new Wpf.Ui.Markup.ThemesDictionary();
                    pink1.Theme = ApplicationTheme.Light;
                    app.Resources.MergedDictionaries.Add(pink1);
                    app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());

                    LoadThemeResources(app, ResourceDictionaryPink);
                    LoadThemeResources(app, ResourceDictionaryBase);
                    break;
                case Theme.Cyan:
                    var Cyan1 = new Wpf.Ui.Markup.ThemesDictionary();
                    Cyan1.Theme = ApplicationTheme.Light;
                    app.Resources.MergedDictionaries.Add(Cyan1);

                    LoadThemeResources(app, ResourceDictionaryCyan);
                    LoadThemeResources(app, ResourceDictionaryBase);
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
        private Theme? _CurrentTheme = Theme.Light;

        //这里是两种
        public Theme CurrentUITheme { get => _CurrentUITheme; private set { if (value == _CurrentUITheme) return; _CurrentUITheme = value; CurrentUIThemeChanged?.Invoke(value);  } }
        private Theme _CurrentUITheme = Theme.Light;


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

        /// <summary>
        /// 预加载主题资源以提高切换性能
        /// </summary>
        public async Task PreloadThemesAsync()
        {
            await Task.Run(() =>
            {
                // 预加载所有主题资源到缓存
                PreloadResourceList(ResourceDictionaryBase);
                PreloadResourceList(ResourceDictionaryDark);
                PreloadResourceList(ResourceDictionaryWhite);
                PreloadResourceList(ResourceDictionaryPink);
                PreloadResourceList(ResourceDictionaryCyan);
            });
        }

        /// <summary>
        /// 预加载资源列表
        /// </summary>
        private void PreloadResourceList(List<string> resources)
        {
            foreach (var uri in resources)
            {
                LoadResourceWithCache(uri);
            }
        }

        /// <summary>
        /// 清理资源缓存
        /// </summary>
        public void ClearResourceCache()
        {
            lock (_cacheLock)
            {
                _resourceCache.Clear();
            }
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public (int Total, int Alive) GetCacheStats()
        {
            lock (_cacheLock)
            {
                int alive = 0;
                foreach (var kvp in _resourceCache)
                {
                    if (kvp.Value.TryGetTarget(out _))
                    {
                        alive++;
                    }
                }
                return (_resourceCache.Count, alive);
            }
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

                case Theme.Cyan:
                    // Disable dark mode
                    attribute = 0;
                    _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref attribute, attributeSize);
                    _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref attribute, attributeSize);

                    // 设置标题栏颜色为 #00796B
                    attribute = 0x6B7900;
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
            ///DWMWA_COLOR_DEFAULT 
            uint attribute = 0xFFFFFFFF; 
            uint attributeSize = (uint)Marshal.SizeOf(typeof(uint));
            //Specifying DWMWA_COLOR_DEFAULT (value 0xFFFFFFFF) for the color will reset the window back to using the system's default behavior for the caption color.
            _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR, ref attribute, attributeSize);
            //Specifying DWMWA_COLOR_NONE (value 0xFFFFFFFE) for the color will suppress the drawing of the window border. This makes it possible to have a rounded window with no border.
            _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, ref attribute, attributeSize);
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref uint pvAttribute, uint cbAttribute);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, IntPtr pvAttribute, uint cbAttribute);

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
    }
}
