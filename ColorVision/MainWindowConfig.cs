using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Configs;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using log4net;
using log4net.Core;
using log4net.Repository.Hierarchy;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision
{
    public class ExportConfigOpen : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "LogOpen";
        public override int Order => 1;
        public override string Header => "打开配置文件(_S)";
        public override void Execute()
        {
            Process.Start("explorer.exe", $"{Path.GetDirectoryName(ConfigHandler.GetInstance().ConfigFilePath)}");
        }
    }

    public class ExportConfigPathOpen : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "LogOpen";
        public override int Order => 2;
        public override string Header => "打开配置文件夹(_F)";
        public override void Execute()
        {
            string fileName = ConfigHandler.GetInstance().ConfigFilePath;
            bool result = Tool.HasDefaultProgram(fileName);
            if (!result)
                Process.Start(result ? "explorer.exe" : "notepad.exe", fileName);
        }
    }


    public class MainWindowConfig : ViewModelBase, IConfig, IConfigSettingProvider,IMenuItemProvider
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MainWindowConfig));

        public static MainWindowConfig Instance => ConfigHandler.GetInstance().GetRequiredService<MainWindowConfig>();

        public bool IsRestoreWindow { get; set; }

        public double Width { get; set; }
        public double Height { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public int WindowState { get; set; }

        public bool IsOpenStatusBar { get => _IsOpenStatusBar; set { _IsOpenStatusBar = value; NotifyPropertyChanged(); } }
        private bool _IsOpenStatusBar = true;
        public bool IsOpenSidebar { get => _IsOpenSidebar; set { _IsOpenSidebar = value; NotifyPropertyChanged(); } }
        private bool _IsOpenSidebar = true;

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

        public static readonly List<string> LogLevels = new() { "all", "debug", "info", "warning", "error", "none" };
        public static IEnumerable<Level> GetAllLevels()
        {
            return new List<Level> { Level.All, Level.Trace, Level.Debug, Level.Info, Level.Warn, Level.Error, Level.Critical, Level.Alert, Level.Fatal, Level.Off };
        }

        private Level _LogLevel = Level.All;
        public Level LogLevel
        {
            get => _LogLevel; set
            {
                _LogLevel = value;
                NotifyPropertyChanged();
                LogLevelName = value.Name;
            }
        }
        public bool AutoScrollToEnd { get => _AutoScrollToEnd; set { _AutoScrollToEnd = value;NotifyPropertyChanged(); } }
        private bool _AutoScrollToEnd = true;



        public bool AutoRefresh { get => _AutoRefresh; set { _AutoRefresh = value; NotifyPropertyChanged(); } }
        private bool _AutoRefresh = true;

        public LogLoadState LogLoadState { get => _LogLoadState; set { _LogLoadState = value; NotifyPropertyChanged(); } }
        private LogLoadState _LogLoadState = LogLoadState.SinceStartup;

        public bool LogReserve { get => _LogReserve; set { _LogReserve = value; NotifyPropertyChanged(); } }
        private bool _LogReserve;

        public string LogLevelName
        {
            get => LogLevel.Name;
            set
            {
                if (value != LogLevel.Name)
                {
                    LogLevel = GetAllLevels().FirstOrDefault(level => level.Name == value) ?? Level.All;
                }
            }
        }



        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            ComboBox cmlog = new ComboBox() { SelectedValuePath = "Key", DisplayMemberPath = "Value" };
            cmlog.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedValueProperty, new Binding(nameof(LogLevel)));

            cmlog.ItemsSource = GetAllLevels().Select(level => new KeyValuePair<Level, string>(level, level.Name));

            cmlog.SelectionChanged += (s, e) => {
                var selectedLevel = (KeyValuePair<Level, string>)cmlog.SelectedItem;
                var hierarchy = (Hierarchy)LogManager.GetRepository();
                if (selectedLevel.Key != hierarchy.Root.Level)
                {
                    hierarchy.Root.Level = selectedLevel.Key;
                    log4net.Config.BasicConfigurator.Configure(hierarchy);
                    log.Info("更新Log4Net 日志级别：" + selectedLevel.Value);
                }
            };
            cmlog.DataContext = Instance;


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
                    Name = Properties.Resources.LogLevel,
                    Description =  Properties.Resources.LogLevel,
                    Order = 15,
                    Type = ConfigSettingType.ComboBox,
                    ComboBox = cmlog,
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
