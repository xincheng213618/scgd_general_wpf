using ColorVision.Common.Utilities;
using ColorVision.FloatingBall;
using ColorVision.UI;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using ColorVision.Windowing;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace ColorVision
{
    public class MainWindowConfig : WindowConfig, IConfigSettingProvider, IFullScreenState
    {
        public static MainWindowConfig Instance => ConfigService.Instance.GetRequiredService<MainWindowConfig>();


        public bool IsOpenStatusBar { get => _IsOpenStatusBar; set { _IsOpenStatusBar = value; OnPropertyChanged(); } }
        private bool _IsOpenStatusBar = true;

        // A new persisted key opts existing installations into the released compact shell.
        // Do not migrate the old experimental UseCompactTitleBar preference.
        [DisplayName("ConfigUseCompactMainWindow")]
        [Description("ConfigUseCompactMainWindowDescription")]
        public bool UseCompactMainWindow { get => _useCompactMainWindow; set { _useCompactMainWindow = value; OnPropertyChanged(); } }
        private bool _useCompactMainWindow = true;

        [Browsable(false)]
        public int LastSeenNewUserGuideVersion { get => _lastSeenNewUserGuideVersion; set { _lastSeenNewUserGuideVersion = value; OnPropertyChanged(); } }
        private int _lastSeenNewUserGuideVersion;

        [JsonIgnore]
        public bool IsFull { get => _IsFull; set { _IsFull = value; OnPropertyChanged(); } }
        private bool _IsFull;

        [DisplayName("ConfigEnableDesktopPet")]
        [Description("ConfigEnableDesktopPetDescription")]
        public bool OpenFloatingBall { get => _OpenFloatingBall; set { _OpenFloatingBall = value; OnPropertyChanged(); FloatingBall(); } }
        private bool _OpenFloatingBall;

        private void FloatingBall()
        {
            if (OpenFloatingBall)
            {
                DesktopPetService.GetInstance().Show();
            }
            else
            {
                DesktopPetService.GetInstance().Hide();
            }
        }


        public const string AutoRunRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string AutoRunName = "ColorVisionAutoRun";

        [JsonIgnore]
        [DisplayName("TbSettingsStartBoot")]
        public bool IsAutoRun { get => Tool.IsAutoRun(AutoRunName, AutoRunRegPath); set { Tool.SetAutoRun(value, AutoRunName, AutoRunRegPath); OnPropertyChanged(); } }

        [JsonIgnore]
        [DisplayName("Win10ClassicDesktopMenu")]
        [Description("Win10ClassicDesktopMenuDescription")]
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
                    Name = Properties.Resources.ConfigDesktopPet,
                    Type = ConfigSettingType.TabItem,
                    Source = DesktopPetConfig.Instance,
                    ViewType = typeof(DesktopPetSettingsControl),
                    Order = 20,
                },
            };

            list.AddRange(CreateWindowAppearanceSettings(Instance,
                ShouldShowCompactMainWindowSetting(CompactTitleBarChrome.IsSupportedOperatingSystem), Tool.IsWin11));
            return list;
        }

        internal static IEnumerable<ConfigSettingMetadata> CreateWindowAppearanceSettings(
            MainWindowConfig source, bool showCompactMainWindow, bool showWindows10ContextMenu)
        {
            yield return CreateAppearanceSetting(nameof(IsRestoreWindow), source, -20);
            if (showCompactMainWindow)
                yield return CreateAppearanceSetting(nameof(UseCompactMainWindow), source, -10);
            if (showWindows10ContextMenu)
                yield return CreateAppearanceSetting(nameof(IsWindows10ContextMenu), source, 0);
        }

        private static ConfigSettingMetadata CreateAppearanceSetting(string bindingName, MainWindowConfig source, int order) => new()
        {
            BindingName = bindingName,
            Source = source,
            Section = ConfigSettingConstants.SectionAppearance,
            Order = order,
        };

        internal static bool ShouldShowCompactMainWindowSetting(bool operatingSystemSupported) =>
            operatingSystemSupported;
    }

    public class ExportMenuViewStatusBar : MenuItemBase,IHotKey
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => Properties.Resources.MenuViewStatusBar;

        public HotKeys HotKeys => new(Properties.Resources.MenuViewStatusBar, new Hotkey(), Execute) { Description = BuiltInHotkeyDescriptions.ToggleStatusBar };

        public override void Execute()
        {
            MainWindowConfig.Instance.IsOpenStatusBar = !MainWindowConfig.Instance.IsOpenStatusBar;
            MenuManager.GetInstance().RefreshMenuItemsByGuid(OwnerGuid);

        }
        public override bool? IsChecked => MainWindowConfig.Instance.IsOpenStatusBar ? true : null;

    }
}
