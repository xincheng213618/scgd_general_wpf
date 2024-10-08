using ColorVision.Common.Utilities;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace WindowsServicePlugin
{
    public class InstallService : MenuItemBase, IWizardStep
    {
        public override string OwnerGuid => "ServiceLog";

        public override string GuidId => "InstallService";

        public override int Order => 3;
        public override string Header => "InstallService";

        public string Description => "下载最近的服务压缩包，安装服务时请先解压到需要安装的位置在下载服务管理工具，配置路径到该位置，推荐放在统一文件夹下";

        public DownloadFile DownloadFile { get; set; } = new DownloadFile();
        public InstallService()
        {
            DownloadFile = new DownloadFile();
            DownloadFile.DownloadTile = "下载最新的Service";
        }

        private string url = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/CVWindowsService/CVWindowsService%5B1.7.0.929%5D-0929.rar";
        private string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + @"ColorVision\CVWindowsService[1.7.0.929]-0929.rar";
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
                    PlatformHelper.Open(downloadPath);
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
