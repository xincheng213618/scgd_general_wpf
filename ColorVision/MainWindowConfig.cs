using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Configs;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision
{
    public class MainWindowConfigProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = Properties.Resources.StartRecoverUILayout,
                                Description = Properties.Resources.StartRecoverUILayout,
                                Type = ConfigSettingType.Bool,
                                BindingName = nameof(MainWindowConfig.IsRestoreWindow),
                                Source = MainWindowConfig.Instance
                            }
            };
        }
    }

    public class ExportMainWindowConfig : IMenuItemMeta
    {
        public string? OwnerGuid => "View";
        public string? GuidId => "MenuViewStatusBar";
        public int Order => 10000;
        public string? Header => Properties.Resources.MenuViewStatusBar;
        public MenuItem MenuItem
        {
            get
            {
                MenuItem MenuDump = new() { Header = Header };
                MenuDump.SetBinding(MenuItem.IsCheckedProperty, new Binding(nameof(MainWindowConfig.IsOpenStatusBar)));
                MenuDump.Click += (s,e) => MainWindowConfig.Instance.IsOpenStatusBar = !MainWindowConfig.Instance.IsOpenStatusBar;
                MenuDump.DataContext = MainWindowConfig.Instance;
                return MenuDump;
            }
        }
        public string? InputGestureText => null;
        public object? Icon => null;
        public RelayCommand Command => null;
        public Visibility Visibility => Visibility.Visible;
    }


    public class MainWindowConfig : ViewModelBase, IConfig
    {
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
    }
}
