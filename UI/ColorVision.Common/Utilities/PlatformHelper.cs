using IWshRuntimeLibrary;
using System;
using System.Diagnostics;
using System.Windows;

namespace ColorVision.Common.Utilities
{

    public static class PlatformHelper
    {
        /// <summary>
        /// 打开文件夹
        /// </summary>
        /// <param name="folder">路径</param>
        public static void OpenFolder(string? folder)
        {
            if (folder == null) return;
            folder = folder.Replace("\\\\", "\\");
            if (OperatingSystem.IsWindows())
            {
                Process.Start("explorer.exe", $"{folder}");
            }

            if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", $"\"{folder}\"");
            }

            if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", $"\"{folder}\"");
            }
        }
        public static void OpenFolderAndSelectFile(string filePath)
        {
            if (filePath == null) return;

            filePath = filePath.Replace("\\\\", "\\");

            var psi = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,{filePath}",
                UseShellExecute = true
            };
            Process.Start(psi);

        }


        /// <summary>
        /// 打开各种 (文件、url)
        /// </summary>
        /// <param name="filename">文件名</param>
        public static void Open(string filename)
        {
            if (filename == null) return;
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    Process.Start(new ProcessStartInfo(filename) { UseShellExecute = true });
                }
                if (OperatingSystem.IsMacOS())
                {
                    Process.Start("open", $"\"{filename}\"");
                }

                if (OperatingSystem.IsLinux())
                {
                    Process.Start("xdg-open", $"\"{filename}\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }
    }
}
