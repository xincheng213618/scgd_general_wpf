#pragma warning disable SYSLIB0014
using ColorVision.Common.MVVM;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Menus;
using log4net;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;
using WindowsServicePlugin.CVWinSMS;


namespace WindowsServicePlugin.Tools
{
    public class MenuBeyondCompare : MenuItemBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MenuBeyondCompare));
        public override string OwnerGuid => MenuItemConstants.View;
        public override int Order => 99;

        public override string Header => "打开BeyondCompare";

        public DownloadFile DownloadFile { get; set; } = new DownloadFile();

        public static string LatestReleaseUrl => "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/BeyondCompare/LATEST_RELEASE";
        private string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + @"ColorVision\\";

        public MenuBeyondCompare()
        {
            DownloadFile = new DownloadFile();
            DownloadFile.DownloadTile = "下载BeyondCompare";
        }
        public override void Execute()
        {
            if (!File.Exists(ImageJConfig.Instance.BeyondComparePath))
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), "找不到BCompare，是否下载", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    string filename = "Beyond_Compare_5.zip";
                    string url = $"http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/BeyondCompare/{filename}";
                    downloadPath = Path.Combine(downloadPath, filename);
                    Task.Run(() =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
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

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + @"ColorVision\\";
                                    ZipFile.ExtractToDirectory(downloadPath, path, true);

                                    ImageJConfig.Instance.BeyondComparePath = path + "\\Beyond Compare 5\\BCompare.exe";

                                    // 启动新的实例
                                    ProcessStartInfo startInfo = new();
                                    startInfo.UseShellExecute = true; // 必须为true才能使用Verb属性
                                    startInfo.WorkingDirectory = Environment.CurrentDirectory;
                                    startInfo.FileName = ImageJConfig.Instance.BeyondComparePath;

                                    try
                                    {
                                        Process p = Process.Start(startInfo);
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show(ex.ToString());
                                        File.Delete(downloadPath);
                                    }

                                });

                            });
                        });


                    });
                    return;
                }

                if (MessageBox.Show(Application.Current.GetActiveWindow(), "I can't find BCompare (BCompare.exe). Would you like to help me find it?", "Open in BCompare", MessageBoxButton.YesNo) == MessageBoxResult.No) return;
                using (System.Windows.Forms.OpenFileDialog openFileDialog = new())
                {
                    openFileDialog.Title = "Select BCompare.exe";
                    openFileDialog.Filter = "BCompare.exe|BCompare.exe";
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    ImageJConfig.Instance.BeyondComparePath = openFileDialog.FileName;
                }
            }

            ProcessStartInfo startInfo = new();
            startInfo.UseShellExecute = true; // 必须为true才能使用Verb属性
            startInfo.WorkingDirectory = Environment.CurrentDirectory;
            startInfo.FileName = ImageJConfig.Instance.BeyondComparePath;
            //startInfo.Verb = "runas"; // "runas"指定启动程序时请求管理员权限
            //                          // 如果需要静默安装，添加静默安装参数
            //                          //quiet 没法自启，桌面图标也是空                       
            //                          //startInfo.Arguments = "/quiet";
            try
            {
                Process p = Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
            }
        }


    }
}
