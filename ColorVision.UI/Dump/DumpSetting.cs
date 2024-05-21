#pragma warning disable CS8603
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using Microsoft.Win32;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Dump
{
    public enum DumpType
    {
        //自定义转储
        Custom = 0,
        //小型转储 
        Mini = 1,
        //完全转储
        Full = 2
    }

    public class DumpConfig : IConfig
    {
        public static DumpConfig Instance => ConfigHandler.GetInstance().GetRequiredService<DumpConfig>();

        private static string Name => Assembly.GetEntryAssembly()?.GetName().Name;

        private static string RegistryKeyPath => $@"SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\{Name}.exe";
        private static string RegistryDefaultKeyPath => @"SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps";

        public DumpConfig()
        {
            LoadRegistryValues(RegistryDefaultKeyPath);
            LoadRegistryValues(RegistryKeyPath);
        }

        private void LoadRegistryValues(string path)
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(path))
            {
                if (key == null) return;

                DumpFolder = key.GetValue("DumpFolder")?.ToString() ?? DumpFolder;
                DumpCount = key.GetValue("DumpCount") is int count ? count : DumpCount;
                DumpType = key.GetValue("DumpType") is int type ? (DumpType)type : DumpType;
            }
        }
        public string DumpFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "CrashDumps");
        public DumpType DumpType { get; set; } = DumpType.Mini;
        public int DumpCount { get; set; } = 10;

        public void SetDump()
        {
            if (!Tool.IsAdministrator())
            {
                MessageBox.Show("操作需要使用管理员权限");
                return;
            }
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(RegistryKeyPath))
            {
                if (key == null) return;

                key.SetValue("DumpFolder", DumpFolder, RegistryValueKind.ExpandString);
                key.SetValue("DumpCount", DumpCount, RegistryValueKind.DWord);
                key.SetValue("DumpType", (int)DumpType, RegistryValueKind.DWord);
            }
        }


        public  void ClearDump()
        {
            if (!Tool.IsAdministrator())
            {
                MessageBox.Show("操作需要使用管理员权限");
                return;
            }
            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(RegistryKeyPath, false);
                LoadRegistryValues(RegistryDefaultKeyPath);
            }
            catch (ArgumentException)
            {
                // 可能会遇到注册表项不存在的情况，这里忽略该异常
            }
        }
    }


    public class MenuDumpConfig : IMenuItemMeta
    {
        public string? OwnerGuid => "Help";

        public string? GuidId => "MenuDumpConfig";

        public int Order => 10000;

        public string? Header => "转储文件设置";

        public MenuItem MenuItem
        {
            get
            {
                MenuItem MenuDump = new() { Header = Header };

                foreach (var item in Enum.GetValues(typeof(DumpType)).Cast<DumpType>())
                {
                    MenuItem ThemeItem = new();
                    ThemeItem.Header = item;
                    ThemeItem.Click += (s, e) =>
                    {
                        DumpConfig.Instance.DumpType = item;
                        DumpConfig.Instance.SetDump();
                    };
                    ThemeItem.Tag = item;
                    ThemeItem.IsChecked = DumpConfig.Instance.DumpType == item;
                    MenuDump.Items.Add(ThemeItem);
                }
                MenuItem Open = new MenuItem() { Header = "打开Dump文件夹", Command = Command };
                MenuDump.Items.Add(Open);

                RelayCommand relayCommand = new RelayCommand(A => DumpConfig.Instance.ClearDump());
                MenuItem Clear = new MenuItem() { Header = "设置为默认", Command = relayCommand };
                MenuDump.Items.Add(Clear);

                MenuDump.Loaded += (s, e) =>
                {
                    foreach (var item in MenuDump.Items.OfType<MenuItem>())
                    {
                        if (item.Tag is DumpType DumpType)
                            item.IsChecked = DumpConfig.Instance.DumpType == DumpType;
                    }
                };
                return MenuDump;
            }
        }


        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new(A => Execute());

        public Visibility Visibility => Visibility.Visible;

        private static void Execute()
        {
            if (Directory.Exists(DumpConfig.Instance.DumpFolder))
                PlatformHelper.OpenFolder(DumpConfig.Instance.DumpFolder);
        }
    }
}
