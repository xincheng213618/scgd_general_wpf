using System;
using System.Diagnostics;
using System.Windows;

namespace ColorVision.Common.Utilities;

public static class PlatformHelper
{
    /// <summary>
    /// 打开文件夹
    /// </summary>
    /// <param name="folder">路径</param>
    public static void OpenFolder(string folder)
    {
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
    public static readonly bool IsWin10 = !(Environment.OSVersion.Version >= new Version(10, 0, 21996)) && Environment.OSVersion.Version >= new Version(10, 0);

    /// <summary>
    /// 打开各种 (文件、url)
    /// </summary>
    /// <param name="filename">文件名</param>
    public static void Open(string filename)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (Environment.OSVersion.Version >= new Version(10, 0, 21996))
                {
                    Process.Start(new ProcessStartInfo(filename) { UseShellExecute = true });
                }
                else
                {
                    Process.Start("explorer.exe", $"{filename}");
                }
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
        catch(Exception ex)
        {
            MessageBox.Show(ex.Message);
        }

    }
}