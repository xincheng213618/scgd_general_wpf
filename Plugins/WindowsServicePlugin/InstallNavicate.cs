using ColorVision.Themes.Controls;
using ColorVision.UI;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace WindowsServicePlugin
{
    public class InstallNavicate : WizardStepBase
    {
        public override int Order => 50;
        public override string Header => ColorVision.UI.Properties.Resources.Download +"Navicate";
        public override string Description => "Download Navicat as a third-party client for database management.";

        public DownloadFile DownloadFile { get; set; } = new DownloadFile();
        public InstallNavicate()
        {
            DownloadFile = new DownloadFile();
            DownloadFile.DownloadTile = ColorVision.UI.Properties.Resources.Download+"Navicate";
        }

        private string url = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/navicat/navicat161_premium_cs_x64.exe";
        private string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + @"ColorVision\\navicat161_premium_cs_x64.exe";

        public override void Execute()
        {
            WindowUpdate windowUpdate = new WindowUpdate(DownloadFile) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            if (!File.Exists(downloadPath))
            {
                windowUpdate.Show();
            }

            Task.Run(async () =>
            {
                if (!File.Exists(downloadPath))
                {
                    CancellationTokenSource _cancellationTokenSource = new();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        windowUpdate.Show();
                    });
                    await DownloadFile.Download(url, downloadPath, _cancellationTokenSource.Token);
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    windowUpdate.Close();
                });
                // 启动新的实例
                ProcessStartInfo startInfo = new();
                startInfo.UseShellExecute = true; // 必须为true才能使用Verb属性
                startInfo.WorkingDirectory = Environment.CurrentDirectory;
                startInfo.FileName = downloadPath;
                startInfo.Verb = "runas"; // "runas"指定启动程序时请求管理员权限
                                          // 如果需要静默安装，添加静默安装参数
                                          //quiet 没法自启，桌面图标也是空                       
                                          //startInfo.Arguments = "/quiet";

                try
                {
                    Process p = Process.Start(startInfo);
                    p?.WaitForExit();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                    File.Delete(downloadPath);
                }
            });


        }

    }



}
