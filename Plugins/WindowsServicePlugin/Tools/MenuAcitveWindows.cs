#pragma warning disable SYSLIB0014
using ColorVision.ImageEditor;
using ColorVision.UI.Menus;
using log4net;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;


namespace WindowsServicePlugin.Tools
{
    public class MenuAcitveWindows : MenuItemBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MenuAcitveWindows));

        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "激活Windows";
        public override int Order => 99;

        public override void Execute()
        {
            log.Info("irm https://get.activated.win | iex");
            string resourceName = "WindowsServicePlugin.Assets.activate.ps1"; // 注意替换为实际命名空间和资源路径
            string tempScriptPath = Path.Combine(Path.GetTempPath(), "activate.ps1");
            
            try
            {
                // 释放嵌入资源到临时脚本
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                using (FileStream fileStream = new FileStream(tempScriptPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fileStream);
                }

                ProcessStartInfo startInfo = new()
                {
                    UseShellExecute = true,
                    WorkingDirectory = @"C:\Windows\System32",
                    FileName = "powershell.exe",
                    Verb = "runas",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempScriptPath}\"",
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process process = Process.Start(startInfo);
                process?.WaitForExit();

                // 执行完毕后删除临时脚本文件
                if (File.Exists(tempScriptPath))
                {
                    File.Delete(tempScriptPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
