using ColorVision.Common.Utilities;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace WindowsServicePlugin
{
    public class InstallWinrar : MenuItemBase, IWizardStep
    {
        public override string OwnerGuid => "ServiceLog";

        public override string GuidId => "InstallWinrar";

        public override int Order => -1;
        public override string Header => "InstallWinrar";


        public DownloadFile DownloadFile { get; set; } = new DownloadFile();

        public string Description => "下载并安装Winrar7.0 作为默认的解压软件"; 

        public InstallWinrar()
        {
            DownloadFile = new DownloadFile();
            DownloadFile.DownloadTile = "InstallWinrar";
        }

        private string url = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/Winrar/winrar-x64-700sc.exe";
        private string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + @"ColorVision\\winrar-x64-700sc.exe";

        public override void Execute()
        {
            WindowUpdate windowUpdate = new WindowUpdate(DownloadFile);
            if (!File.Exists(downloadPath))
            {
                windowUpdate.Show();
            }

            Task.Run(async () =>
            {
                if (!File.Exists(downloadPath))
                {
                    await DownloadFile.GetIsPassWorld();
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
