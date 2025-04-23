using ColorVision.Common.Utilities;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using System.IO;
using System.Windows;

namespace WindowsServicePlugin
{
    public class InstallMySql : WizardStepBase
    {

        public override int Order => 99;
        public override string Header => "下载MySql";

        public override string Description => "下载mysql-5.7.37-winx64压缩包到本地，后续可以在管理工具中选择并安装";

        public DownloadFile DownloadFile { get; set; } = new DownloadFile();

        public InstallMySql()
        {
            DownloadFile = new DownloadFile();
            DownloadFile.DownloadTile = "下载mysql-5.7.37-winx64";
        }

        private string url = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/Mysql/mysql-5.7.37-winx64.zip";
        private string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + @"ColorVision\\mysql-5.7.37-winx64.zip";

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

                try
                {
                    PlatformHelper.OpenFolder(Directory.GetParent(downloadPath).FullName);
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
