#pragma warning disable SYSLIB0014
using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using System.Diagnostics;
using System.Windows;


namespace WindowsServicePlugin.Tools
{
    public class MenuAcitveWindows : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "激活Windows";
        public override int Order => 99;

        public override void Execute()
        {
            ProcessStartInfo startInfo = new()
            {
                UseShellExecute = true,
                WorkingDirectory = @"C:\Windows\System32",
                FileName = "powershell.exe",
                Verb = "runas", // 请求管理员权限
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command " + "irm https://get.activated.win | iex", // PowerShell 命令
                WindowStyle = ProcessWindowStyle.Normal // 隐藏命令行窗口
            };

            try
            {
                Process process = Process.Start(startInfo);
                process?.WaitForExit();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
