#pragma warning disable CS8603
using ColorVision.Common.NativeMethods;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using Microsoft.Win32;
using System.IO;
using System.Reflection;
using System.Windows;

namespace EventVWR.Dump
{
    public class DumpConfig : IConfig
    {
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
                CustomDumpFlags = key.GetValue("CustomDumpFlags") is int minidumpType ? (MinidumpType)minidumpType : CustomDumpFlags;
            }
        }

        public MinidumpType CustomDumpFlags { get; set; } = MinidumpType.MiniDumpNormal;

        public string DumpFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "CrashDumps");
        public DumpType DumpType { get; set; } = DumpType.Mini;
        public int DumpCount { get; set; } = 10;

        public void SetDump()
        {
            if (!Tool.IsAdministrator())
            {
                MessageBox.Show(Application.Current.GetActiveWindow(),"操作需要使用管理员权限", Name);
                return;
            }
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(RegistryKeyPath))
            {
                if (key == null) return;

                key.SetValue("DumpFolder", DumpFolder, RegistryValueKind.ExpandString);
                key.SetValue("DumpCount", DumpCount, RegistryValueKind.DWord);
                key.SetValue("DumpType", (int)DumpType, RegistryValueKind.DWord);

                if (DumpType == DumpType.Custom)
                    key.SetValue("CustomDumpFlags", (int)CustomDumpFlags, RegistryValueKind.DWord);
            }
        }

        public void SaveDump()
        {
            DumpHelper.WriteMiniDump( $"{DumpFolder}\\{Name}.exe.dmp");
        }

        public  void ClearDump()
        {
            if (!Tool.IsAdministrator())
            {
                MessageBox.Show(Application.Current.GetActiveWindow(),"操作需要使用管理员权限", Name);
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
}
