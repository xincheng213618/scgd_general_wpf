using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace ColorVision.Common.Utilities
{

    public static class PlatformHelper
    {
        /// <summary>
        /// 打开文件夹；如果文件夹窗口已打开，则激活并置顶该窗口。
        /// </summary>
        /// <param name="folder">路径</param>
        public static void OpenFolder(string? folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return;

            try
            {
                // 归一化路径
                folder = NormalizePath(folder);
                if (!Directory.Exists(folder)) return;

                if (OperatingSystem.IsWindows())
                {
                    OpenOrActivateOnWindows(folder);
                    return;
                }

                if (OperatingSystem.IsMacOS())
                {
                    OpenOrActivateOnMac(folder);
                    return;
                }

                if (OperatingSystem.IsLinux())
                {
                    // Linux 桌面环境及文件管理器多样，无法可靠检测“已打开”的窗口，采用最佳努力打开
                    Process.Start("xdg-open", folder);
                    return;
                }
            }
            catch
            {
                // 忽略错误，尽量不影响主流程
            }
        }

        private static string NormalizePath(string path)
        {
            // 把重复的反斜杠压缩，并取绝对路径
            path = path.Replace("\\\\", "\\").Trim();
            // 在 Windows 用反斜杠，其它平台用正斜杠
            if (OperatingSystem.IsWindows())
            {
                path = path.Replace('/', '\\');
            }
            else
            {
                path = path.Replace('\\', '/');
            }
            // GetFullPath 也会移除末尾多余分隔符
            var full = Path.GetFullPath(path);
            // 比较时移除末尾分隔符
            return full.TrimEnd(Path.DirectorySeparatorChar);
        }

        // ====================== Windows ======================
        private static void OpenOrActivateOnWindows(string folder)
        {
            // 首先尝试找到已打开的资源管理器窗口并激活
            if (TryActivateExistingExplorerWindow(folder))
            {
                return;
            }

            // 未找到则新开
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = folder,
                UseShellExecute = true
            });
        }

        private static bool TryActivateExistingExplorerWindow(string folder)
        {
            try
            {
                // 使用 COM: Shell.Application.Windows() 枚举现有资源管理器窗口（含 IE，但我们只关心 Explorer）
                var shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return false;

                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null) return false;

                dynamic windows = shell.Windows();
                int count = (int)windows.Count;

                for (int i = 0; i < count; i++)
                {
                    dynamic w = windows.Item(i);
                    // 可能为 IE 窗口，LocationURL 为 http(s)，跳过
                    string? locationUrl = null;
                    try { locationUrl = (string?)w.LocationURL; } catch { /* ignore */ }

                    if (string.IsNullOrEmpty(locationUrl)) continue;
                    if (!locationUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase)) continue;

                    string? localPath = null;
                    try
                    {
                        localPath = Uri.UnescapeDataString(new Uri(locationUrl).LocalPath)
                            .TrimEnd(Path.DirectorySeparatorChar);
                    }
                    catch
                    {
                        continue;
                    }

                    if (localPath == null) continue;

                    if (string.Equals(localPath, folder, StringComparison.OrdinalIgnoreCase))
                    {
                        // 拿到窗口句柄，激活并置顶
                        IntPtr hwnd = IntPtr.Zero;
                        try
                        {
                            // HWND 在 COM 中是 int 类型
                            int raw = (int)w.HWND;
                            hwnd = new IntPtr(raw);
                        }
                        catch
                        {
                            // 如果拿不到句柄，则无法激活
                            return false;
                        }

                        RestoreAndActivateWindow(hwnd);
                        return true;
                    }
                }
            }
            catch
            {
                // 某些环境下 COM 不可用，忽略
            }

            return false;
        }

        private static void RestoreAndActivateWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            // 如果最小化，先还原
            if (IsIconic(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
            }
            else
            {
                ShowWindow(hWnd, SW_SHOW);
            }

            // 置前台
            SetForegroundWindow(hWnd);
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        // ====================== macOS ======================
        private static void OpenOrActivateOnMac(string folder)
        {
            // AppleScript：若 Finder 已打开该文件夹，则把该窗口置顶；否则打开并激活
            // 注意：使用 POSIX file 转 alias 来与窗口 target 做等值比较
            string script = $@"
tell application ""Finder""
    set theFolder to POSIX file ""{EscapeAppleScript(folder)}"" as alias
    set found to false
    repeat with w in windows
        try
            if ((target of w) as alias) is theFolder then
                set index of w to 1
                set found to true
                exit repeat
            end if
        end try
    end repeat
    if found then
        activate
    else
        reveal theFolder
        activate
    end if
end tell";

            RunOsascript(script);
        }

        private static void RunOsascript(string script)
        {
            try
            {
                using var p = new Process();
                p.StartInfo = new ProcessStartInfo
                {
                    FileName = "osascript",
                    ArgumentList = { "-e", script },
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true
                };
                p.Start();
            }
            catch
            {
                // 如果 AppleScript 执行失败，回退到 open
                Process.Start("open", script);
            }
        }

        private static string EscapeAppleScript(string s)
        {
            // 仅需转义双引号与反斜杠
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
        public static void OpenFolderAndSelectFile(string? filePath)
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
