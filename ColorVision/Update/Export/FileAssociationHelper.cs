using ColorVision.Common.Utilities;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace ColorVision.Update.Export
{
    public class MenuFileAssociation : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);
        public override int Order => 1000;
        public override string Header => "FileAssociation";
        public override void Execute()
        {
            FileAssociationHelper.RegisterAssociations();
            MessageBox.Show(Application.Current.GetActiveWindow(),"注册表应用成功","ColorVision");
        }
    }

    public static class FileAssociationHelper
    {
        /// <summary>
        /// 生成注册表文件并请求管理员权限导入
        /// </summary>
        public static bool RegisterAssociations()
        {
            try
            {
                // 1. 获取当前程序所在目录和exe路径
                string appPath = Process.GetCurrentProcess().MainModule.FileName;
                string appDir = Path.GetDirectoryName(appPath);
                string iconPath = Path.Combine(appDir, "ColorVisionIcons64.dll");

                // 2. 为了写入 .reg 文件，路径中的反斜杠需要转义 (例如 C:\Program 变成 C:\\Program)
                string escapedAppPath = appPath.Replace("\\", "\\\\");
                string escapedIconPath = iconPath.Replace("\\", "\\\\");

                // 3. 构建 .reg 文件内容
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Windows Registry Editor Version 5.00");
                sb.AppendLine();

                // ------------------------------------------------------
                //  1. 注册 .cvx (Xincheng Core Update / 本体更新)
                // ------------------------------------------------------
                sb.AppendLine($"[HKEY_CLASSES_ROOT\\.cvx]");
                sb.AppendLine($"@=\"ColorVision.Update.cvx\""); // 指向 ProgID
                sb.AppendLine();

                sb.AppendLine($"[HKEY_CLASSES_ROOT\\ColorVision.Update.cvx]");
                sb.AppendLine($"@=\"ColorVision Core Update Package\""); // 文件类型描述
                sb.AppendLine();

                // 图标 (使用 index 0)
                sb.AppendLine($"[HKEY_CLASSES_ROOT\\ColorVision.Update.cvx\\DefaultIcon]");
                sb.AppendLine($"@=\"{escapedIconPath},0\"");
                sb.AppendLine();

                // 双击打开行为 (Open) -> 传入 -i 参数
                sb.AppendLine($"[HKEY_CLASSES_ROOT\\ColorVision.Update.cvx\\shell\\open\\command]");
                sb.AppendLine($"@=\"\\\"{escapedAppPath}\\\" -i \\\"%1\\\"\"");
                sb.AppendLine();


                // ------------------------------------------------------
                //  2. 注册 .cvxp (Xincheng Plugin / 插件更新)
                // ------------------------------------------------------
                sb.AppendLine($"[HKEY_CLASSES_ROOT\\.cvxp]");
                sb.AppendLine($"@=\"ColorVision.Plugin.cvxp\"");
                sb.AppendLine();

                sb.AppendLine($"[HKEY_CLASSES_ROOT\\ColorVision.Plugin.cvxp]");
                sb.AppendLine($"@=\"ColorVision Plugin Package\"");
                sb.AppendLine();

                // 图标 (这里也设置为 0，如果你想区分，可以把 dll 里的 index 改成 1 或其他)
                sb.AppendLine($"[HKEY_CLASSES_ROOT\\ColorVision.Plugin.cvxp\\DefaultIcon]");
                sb.AppendLine($"@=\"{escapedIconPath},0\"");
                sb.AppendLine();

                // 双击打开行为 (Open) -> 传入 -i 参数
                // 注意：如果想区分逻辑，代码里解析 -i 后判断文件后缀即可
                sb.AppendLine($"[HKEY_CLASSES_ROOT\\ColorVision.Plugin.cvxp\\shell\\open\\command]");
                sb.AppendLine($"@=\"\\\"{escapedAppPath}\\\" -i \\\"%1\\\"\"");
                sb.AppendLine();

                // 4. 保存为临时 .reg 文件
                string tempRegFile = Path.Combine(Path.GetTempPath(), $"CV_Register_{Guid.NewGuid()}.reg");
                File.WriteAllText(tempRegFile, sb.ToString(), Encoding.Unicode); // 注册表文件通常推荐 Unicode

                // 5. 调用 regedit.exe 以管理员权限运行 (/s 为静默模式，不弹成功提示框)
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "regedit.exe";
                psi.Arguments = $"/s \"{tempRegFile}\""; // /s 代表 silent，只弹UAC，不弹完成提示
                psi.UseShellExecute = true;
                psi.Verb = "runas"; // 关键：这会触发 UAC 提权提示

                Process process = Process.Start(psi);
                process?.WaitForExit();
                return true;

                // 可选：稍后删除临时文件（由于 regedit 是异步的，立即删除可能导致未读取，实际中可以不删或延迟删除）
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
