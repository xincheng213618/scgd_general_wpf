using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Configs;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision
{
    public class MainWindowConfig : ViewModelBase, IConfig, IConfigSettingProvider,IMenuItemProvider, IFullScreenState
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MainWindowConfig));

        public static MainWindowConfig Instance => ConfigService.Instance.GetRequiredService<MainWindowConfig>();

        public bool IsRestoreWindow { get; set; } = true;

        public double Width { get; set; }
        public double Height { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public int WindowState { get; set; }

        public bool IsOpenStatusBar { get => _IsOpenStatusBar; set { _IsOpenStatusBar = value; NotifyPropertyChanged(); } }
        private bool _IsOpenStatusBar = true;
        public bool IsOpenSidebar { get => _IsOpenSidebar; set { _IsOpenSidebar = value; NotifyPropertyChanged(); } }
        private bool _IsOpenSidebar = true;
        [JsonIgnore]
        public bool IsFull { get => _IsFull; set { _IsFull = value; NotifyPropertyChanged(); } }
        private bool _IsFull;

        public Version? LastOpenVersion { get => _Version; set { _Version = value; NotifyPropertyChanged(); } }
        private Version? _Version = new Version(0, 0, 0, 0);



        public void SetWindow(Window window)
        {
            if (IsRestoreWindow && Height != 0 && Width != 0)
            {
                window.Top = Top;
                window.Left = Left;
                window.Height = Height;
                window.Width = Width;
                window.WindowState = (WindowState)WindowState;

                if (Width > SystemParameters.WorkArea.Width)
                {
                    window.Width = SystemParameters.WorkArea.Width;
                }
                if (Height > SystemParameters.WorkArea.Height)
                {
                    window.Height = SystemParameters.WorkArea.Height;
                }
            }
        }
        public void SetConfig(Window window)
        {
            Top = window.Top;
            Left = window.Left;
            Height = window.Height;
            Width = window.Width;
            WindowState = (int)window.WindowState;
        }

        public bool OpenFloatingBall { get => _OpenFloatingBall; set { _OpenFloatingBall = value; NotifyPropertyChanged(); } }
        private bool _OpenFloatingBall;

        public const string AutoRunRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string AutoRunName = "ColorVisionAutoRun";
        public bool IsAutoRun { get => Tool.IsAutoRun(AutoRunName, AutoRunRegPath); set { Tool.SetAutoRun(value, AutoRunName, AutoRunRegPath); NotifyPropertyChanged(); } }






        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    Name = Properties.Resources.TbSettingsStartBoot,
                    Description =  Properties.Resources.TbSettingsStartBoot,
                    Order = 15,
                    Type = ConfigSettingType.Bool,
                    BindingName =nameof(IsAutoRun),
                    Source = this,
                },
                new ConfigSettingMetadata
                {
                    Name = Properties.Resources.StartRecoverUILayout,
                    Description = Properties.Resources.StartRecoverUILayout,
                    Type = ConfigSettingType.Bool,
                    BindingName = nameof(IsRestoreWindow),
                    Source = Instance
                }
            };
        }


        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            return new List<MenuItemMetadata>
            {

            };
        }
    }

    public class ExportMenuViewMax :IMenuItemMeta
    {
        public string? OwnerGuid => "View";
        public string? GuidId => "MenuViewSidebar";
        public int Order => 1;
        public string? Header => "全屏";

        public MenuItem MenuItem
        {
            get
            {
                MenuItem menuItem = new() { Header = Header };
                menuItem.SetBinding(MenuItem.IsCheckedProperty, new Binding(nameof(MainWindowConfig.IsFull)));
                menuItem.Click += (s, e) => MainWindowConfig.Instance.IsFull = !MainWindowConfig.Instance.IsFull;
                menuItem.DataContext = MainWindowConfig.Instance;
                return menuItem;
            }
        }
        public string? InputGestureText => null;
        public object? Icon => null;
        public RelayCommand Command => null;
        public Visibility Visibility => Visibility.Visible;
        public static void Execute()
        {
            MainWindowConfig.Instance.IsFull = !MainWindowConfig.Instance.IsFull;
        }


    }


    public class ExportMenuViewStatusBar : IMenuItemMeta,IHotKey
    {
        public string? OwnerGuid => "View";
        public string? GuidId => "MenuViewStatusBar";
        public int Order => 2;
        public string? Header => Properties.Resources.MenuViewStatusBar;
        public MenuItem MenuItem
        {
            get
            {
                MenuItem menuItem = new() { Header = Header };
                menuItem.SetBinding(MenuItem.IsCheckedProperty, new Binding(nameof(MainWindowConfig.IsOpenStatusBar)));
                menuItem.Click += (s,e) => MainWindowConfig.Instance.IsOpenStatusBar = !MainWindowConfig.Instance.IsOpenStatusBar;
                menuItem.DataContext = MainWindowConfig.Instance;
                return menuItem;
            }
        }
        public string? InputGestureText => null;
        public object? Icon => null;
        public RelayCommand Command => null;
        public Visibility Visibility => Visibility.Visible;

        public HotKeys HotKeys => new(Properties.Resources.MenuViewStatusBar, new Hotkey(Key.B, ModifierKeys.Control | ModifierKeys.Shift), Execute);

        public static void Execute()
        {
            MainWindowConfig.Instance.IsOpenStatusBar = !MainWindowConfig.Instance.IsOpenStatusBar;
        }

    }
    public class ExportMenuViewSidebar : IMenuItemMeta, IHotKey
    {
        public string? OwnerGuid => "View";
        public string? GuidId => "MenuViewSidebar";
        public int Order => 1;
        public string? Header => Properties.Resources.MenuViewSidebar;
        public MenuItem MenuItem
        {
            get
            {
                MenuItem menuItem = new() { Header = Header };
                menuItem.SetBinding(MenuItem.IsCheckedProperty, new Binding(nameof(MainWindowConfig.IsOpenSidebar)));
                menuItem.Click += (s, e) => MainWindowConfig.Instance.IsOpenSidebar = !MainWindowConfig.Instance.IsOpenSidebar;
                menuItem.DataContext = MainWindowConfig.Instance;
                return menuItem;
            }
        }




        public string? InputGestureText => null;
        public object? Icon => null;
        public RelayCommand Command => null;
        public Visibility Visibility => Visibility.Visible;

        public HotKeys HotKeys => new(Properties.Resources.MenuViewSidebar, new Hotkey(Key.S, ModifierKeys.Control | ModifierKeys.Shift), Execute);

        public static void Execute()
        {
            MainWindowConfig.Instance.IsOpenSidebar = !MainWindowConfig.Instance.IsOpenSidebar;
        }
    }


}
