using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.Utils;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ColorVision.Tools
{
    public class MenuUploadDump : IMenuItem
    {
        public string? OwnerGuid => "Help";

        public string? GuidId => "MenuUploadDump";

        public int Order => 10000;

        public string? Header => "MenuUpload_Dump";

        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new(A => Execute());
        public Visibility Visibility => Visibility.Visible;

        private static void Execute()
        {
            // 获取用户主目录路径
            string userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            //系统默认路径
            string crashDumpsFolder = Path.Combine(userFolder, "AppData", "Local", "CrashDumps");

            string registryKeyPath = @"SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps";
            string registryValueName = "DumpFolder";
            //如果用户进行了修改
            // 打开注册表项
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registryKeyPath))
            {
                if (key != null)
                {
                    // 获取注册表值
                    object registryValue = key.GetValue(registryValueName);

                    if (registryValue != null)
                    {
                        crashDumpsFolder = registryValue.ToString();
                    }
                    else
                    {
                    }
                }
                else
                {
                }
            }
            if(Directory.Exists(crashDumpsFolder))
                PlatformHelper.OpenFolder(crashDumpsFolder);
        }
    }
}
