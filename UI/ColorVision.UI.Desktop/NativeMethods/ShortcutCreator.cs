using System.IO;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Desktop.NativeMethods
{
    internal static class ShortcutCreator
    {
        public static void CreateShortcut(string shortcutName, string shortcutPath, string targetFileLocation, string arguments = "")
        {
            // 检查是否在 Windows 平台上运行，避免在 Linux 运行时崩溃
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // 如果在 Linux 上需要类似功能，通常需要创建 .desktop 文件或符号链接，
                // 这里暂时直接返回，确保不报错。
                return;
            }

            try
            {
                // 使用反射动态获取 WScript.Shell 类型，移除编译时依赖
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;

                dynamic shell = Activator.CreateInstance(shellType);

                // 创建快捷方式路径
                string linkPath = Path.Combine(shortcutPath, shortcutName + ".lnk");

                // 调用 CreateShortcut
                dynamic shortcut = shell.CreateShortcut(linkPath);

                // 设置属性
                shortcut.Description = "ColorVision";
                shortcut.IconLocation = targetFileLocation + ",0";
                shortcut.TargetPath = targetFileLocation;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetFileLocation);
                shortcut.WindowStyle = 1;
                shortcut.Arguments = arguments;

                // 保存
                shortcut.Save();
            }
            catch (Exception)
            {
                // 可以添加日志记录错误，或者忽略
            }
        }

        public static string GetShortcutTargetFile(string shortcutFilename)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return string.Empty;
            }

            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return string.Empty;

                dynamic shell = Activator.CreateInstance(shellType);
                dynamic link = shell.CreateShortcut(shortcutFilename);

                return link.TargetPath;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}