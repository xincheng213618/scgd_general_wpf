using ColorVision.Common.Utilities;
using ColorVision.FloatingBall;
using ColorVision.UI;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace ColorVision
{
    public class MainWindowConfig : WindowConfig, IConfigSettingProvider, IFullScreenState
    {
        public static MainWindowConfig Instance => ConfigService.Instance.GetRequiredService<MainWindowConfig>();


        public bool IsOpenStatusBar { get => _IsOpenStatusBar; set { _IsOpenStatusBar = value; OnPropertyChanged(); } }
        private bool _IsOpenStatusBar = true;

        [JsonIgnore]
        public bool IsOpenSidebar { get => _IsOpenSidebar; set { _IsOpenSidebar = value; OnPropertyChanged(); } }
        private bool _IsOpenSidebar = true;

        [JsonIgnore]
        public bool IsFull { get => _IsFull; set { _IsFull = value; OnPropertyChanged(); } }
        private bool _IsFull;

        public bool OpenFloatingBall { get => _OpenFloatingBall; set { _OpenFloatingBall = value; OnPropertyChanged(); FloatingBall(); } }
        private bool _OpenFloatingBall;

        FloatingBallWindow floatingBallWindow;
        private void FloatingBall()
        {
            if (OpenFloatingBall)
            {
                if (floatingBallWindow == null)
                    floatingBallWindow = new FloatingBallWindow();
                floatingBallWindow.Show();
            }
            else
            {
                if (floatingBallWindow != null)
                {
                    floatingBallWindow.Close();
                    floatingBallWindow = null;
                }
            }
        }


        public const string AutoRunRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string AutoRunName = "ColorVisionAutoRun";

        public int LeftTabControlSelectedIndex { get => _LeftTabControlSelectedIndex; set { _LeftTabControlSelectedIndex = value; OnPropertyChanged(); } }
        private int _LeftTabControlSelectedIndex = 1;

        [JsonIgnore]
        [DisplayName("TbSettingsStartBoot")]
        public bool IsAutoRun { get => Tool.IsAutoRun(AutoRunName, AutoRunRegPath); set { Tool.SetAutoRun(value, AutoRunName, AutoRunRegPath); OnPropertyChanged(); } }

        [JsonIgnore]
        [DisplayName("Win10ClassicDesktopMenu")]
        public bool IsWindows10ContextMenu { get => !Tool.IsWindows11ContextMenu(); set
            {
                if (value != Tool.IsWindows11ContextMenu()) return;
                if (value)
                    Tool.SwitchToWindows10ContextMenu();
                else
                    Tool.SwitchToWindows11ContextMenu();
                OnPropertyChanged(nameof(IsWindows10ContextMenu));
            } 
        }


        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {

            var list = new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    BindingName =nameof(IsAutoRun),
                    Source = Instance,
                },
                new ConfigSettingMetadata
                {
                    BindingName =nameof(OpenFloatingBall),
                    Source = Instance,
                },
                new ConfigSettingMetadata
                {
                    BindingName = nameof(IsRestoreWindow),
                    Source = Instance
                }
            };

            if (Tool.IsWin11)
            {
                list.Add(new ConfigSettingMetadata
                {
                    BindingName = nameof(IsWindows10ContextMenu),
                    Source = Instance,
                });
            }
            return list;
        }
    }

    public class ExportMenuViewMax :MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => ColorVision.Properties.Resources.FullScreen;

        public override void Execute()
        {
            MainWindowConfig.Instance.IsFull = !MainWindowConfig.Instance.IsFull;
            MenuManager.GetInstance().RefreshMenuItemsByGuid(OwnerGuid);
        }
        public override bool? IsChecked => MainWindowConfig.Instance.IsFull ? true : null;
    }



    public class ExportMenuViewStatusBar : MenuItemBase,IHotKey
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => Properties.Resources.MenuViewStatusBar;

        public HotKeys HotKeys => new(Properties.Resources.MenuViewStatusBar, new Hotkey(Key.B, ModifierKeys.Control | ModifierKeys.Shift), Execute);

        public override void Execute()
        {
            MainWindowConfig.Instance.IsOpenStatusBar = !MainWindowConfig.Instance.IsOpenStatusBar;
            MenuManager.GetInstance().RefreshMenuItemsByGuid(OwnerGuid);

        }
        public override bool? IsChecked => MainWindowConfig.Instance.IsOpenStatusBar ? true : null;

    }
    public class ExportMenuViewSidebar : MenuItemBase, IHotKey
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => Properties.Resources.MenuViewSidebar;
        public HotKeys HotKeys => new(Properties.Resources.MenuViewSidebar, new Hotkey(Key.S, ModifierKeys.Control | ModifierKeys.Shift), Execute);

        public override void Execute()
        {
            MainWindowConfig.Instance.IsOpenSidebar = !MainWindowConfig.Instance.IsOpenSidebar;
            MenuManager.GetInstance().RefreshMenuItemsByGuid(OwnerGuid);
        }
        public override bool? IsChecked => MainWindowConfig.Instance.IsOpenSidebar ? true : null;

    }


}
