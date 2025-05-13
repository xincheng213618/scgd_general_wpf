using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace ColorVision
{
    public class MainWindowConfig : WindowConfig, IConfigSettingProvider, IFullScreenState
    {
        public static MainWindowConfig Instance => ConfigService.Instance.GetRequiredService<MainWindowConfig>();

        public bool IsOpenStatusBar { get => _IsOpenStatusBar; set { _IsOpenStatusBar = value; NotifyPropertyChanged(); } }
        private bool _IsOpenStatusBar = true;

        [JsonIgnore]
        public bool IsOpenSidebar { get => _IsOpenSidebar; set { _IsOpenSidebar = value; NotifyPropertyChanged(); } }
        private bool _IsOpenSidebar = true;
        [JsonIgnore]
        public bool IsFull { get => _IsFull; set { _IsFull = value; NotifyPropertyChanged(); } }
        private bool _IsFull;

        public Version? LastOpenVersion { get => _Version; set { _Version = value; NotifyPropertyChanged(); } }
        private Version? _Version = new Version(0, 0, 0, 0);

        public bool OpenFloatingBall { get => _OpenFloatingBall; set { _OpenFloatingBall = value; NotifyPropertyChanged(); } }
        private bool _OpenFloatingBall;

        public const string AutoRunRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string AutoRunName = "ColorVisionAutoRun";

        [JsonIgnore]
        public bool IsAutoRun { get => Tool.IsAutoRun(AutoRunName, AutoRunRegPath); set { Tool.SetAutoRun(value, AutoRunName, AutoRunRegPath); NotifyPropertyChanged(); } }

        public int LeftTabControlSelectedIndex { get => _LeftTabControlSelectedIndex; set { _LeftTabControlSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _LeftTabControlSelectedIndex = 1;


        [JsonIgnore]
        public bool IsWindows10ContextMenu { get => !Tool.IsWindows11ContextMenu(); set
            {
                if (value != Tool.IsWindows11ContextMenu()) return;
                if (value)
                    Tool.SwitchToWindows10ContextMenu();
                else
                    Tool.SwitchToWindows11ContextMenu();
                NotifyPropertyChanged(nameof(IsWindows10ContextMenu));
            } 
        }




        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {

            var list = new List<ConfigSettingMetadata>
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

            if (Tool.IsWin11)
            {
                list.Add(new ConfigSettingMetadata
                {
                    Name = "Win10桌面经典菜单",
                    Description = "",
                    Order = 15,
                    Type = ConfigSettingType.Bool,
                    BindingName = nameof(IsWindows10ContextMenu),
                    Source = this,
                });
            }
            return list;
        }
    }

    public class ExportMenuViewMax :MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => ColorVision.ImageEditor.Properties.Resources.FullScreen;

        public override void Execute()
        {
            MainWindowConfig.Instance.IsFull = !MainWindowConfig.Instance.IsFull;
        }
    }


    public class ExportMenuViewStatusBar : MenuItemBase,IHotKey
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => Properties.Resources.MenuViewStatusBar;

        public HotKeys HotKeys => new(Properties.Resources.MenuViewStatusBar, new Hotkey(Key.B, ModifierKeys.Control | ModifierKeys.Shift), Execute);

        public override void Execute()
        {
            MainWindowConfig.Instance.IsOpenStatusBar = !MainWindowConfig.Instance.IsOpenStatusBar;
        }

    }
    public class ExportMenuViewSidebar : MenuItemBase, IHotKey
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => Properties.Resources.MenuViewSidebar;
        public HotKeys HotKeys => new(Properties.Resources.MenuViewSidebar, new Hotkey(Key.S, ModifierKeys.Control | ModifierKeys.Shift), Execute);

        public override void Execute()
        {
            MainWindowConfig.Instance.IsOpenSidebar = !MainWindowConfig.Instance.IsOpenSidebar;
        }
    }


}
