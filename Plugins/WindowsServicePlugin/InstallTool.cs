#pragma warning disable SYSLIB0014
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;

namespace WindowsServicePlugin
{
    public class CVWinSMSConfig : ViewModelBase, IConfig   
    {
        public static CVWinSMSConfig Instance => ConfigService.Instance.GetRequiredService<CVWinSMSConfig>();

        public string CVWinSMSPath { get => _CVWinSMSPath; set => _CVWinSMSPath = value; }
        private string _CVWinSMSPath = string.Empty;

        public string Version { get => _Version; set => _Version = value; }
        private string _Version = string.Empty;

        public string UpdatePath { get => _UpdatePath; set { _UpdatePath = value; NotifyPropertyChanged(); } }
        private string _UpdatePath = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/InstallTool";
    }

    public class InstallTool : MenuItemBase, IWizardStep
    {


        public override string OwnerGuid => "ServiceLog";

        public override string GuidId => "InstallTool";

        public override int Order => 4;

        public override string Header => Properties.Resources.ManagementService;

        public string Description => "打开最新的服务管理工具，如果不存在会自动下载，下载后请手动指定保存位置";

        public DownloadFile DownloadFile { get; set; } = new DownloadFile();

        public static CVWinSMSConfig Config => CVWinSMSConfig.Instance;
        public static string LatestReleaseUrl => Config.UpdatePath + "/LATEST_RELEASE";


        private string url = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/InstallTool/InstallTool[2.1.0.24111].zip";
        private string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + @"ColorVision\\";


        public InstallTool()
        {
            DownloadFile = new DownloadFile();
            DownloadFile.DownloadTile = "下载服务管理工具";
            Task.Run(() => GetLatestReleaseVersion());
        }

        public async void GetLatestReleaseVersion()
        {
            try
            {
                if (!File.Exists(Config.CVWinSMSPath))
                    return;
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(Config.CVWinSMSPath);
                Version CurrentVerision = new Version(versionInfo.FileVersion);

                Version version = await DownloadFile.GetLatestVersionNumber(LatestReleaseUrl);
                if (version > CurrentVerision)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (MessageBox.Show(Application.Current.GetActiveWindow(), "服务管理工具:找到新版本，是否更新", "CVWinSMS", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + $"ColorVision\\InstallTool[{version}].zip";
                            url = $"http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/InstallTool/InstallTool[{version}].zip";
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

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    Process.GetProcessesByName("CVWinSMS").ToList().ForEach(p => p.Kill());

                                    try
                                    {
                                        string? folderBrowser = Directory.GetParent(Directory.GetParent(CVWinSMSConfig.Instance.CVWinSMSPath)?.FullName)?.FullName;
                                        if (folderBrowser != null)
                                        {
                                            ZipFile.ExtractToDirectory(downloadPath, folderBrowser, true);
                                        }
                                        else
                                        {
                                            MessageBox.Show("更新失败， 找不到更新所在的文件夹");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show("更新失败，" + ex.Message);

                                    }

                                    // 启动新的实例
                                    ProcessStartInfo startInfo = new();
                                    startInfo.UseShellExecute = true; // 必须为true才能使用Verb属性
                                    startInfo.WorkingDirectory = Environment.CurrentDirectory;
                                    startInfo.FileName = CVWinSMSConfig.Instance.CVWinSMSPath;
                                    startInfo.Verb = "runas"; // "runas"指定启动程序时请求管理员权限
                                                              // 如果需要静默安装，添加静默安装参数
                                                              //quiet 没法自启，桌面图标也是空                       
                                                              //startInfo.Arguments = "/quiet";

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

                        };

                    });

                }
            }catch(Exception ex)
            {
                
            }
        }

        public async void Download()
        {
            Version version = await DownloadFile.GetLatestVersionNumber(LatestReleaseUrl);
            downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + $"ColorVision\\InstallTool[{version}].zip";
            url = $"http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/InstallTool/InstallTool[{version}].zip";

            Application.Current.Dispatcher.Invoke(() =>
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

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Process.GetProcessesByName("CVWinSMS").ToList().ForEach(p => p.Kill());
                        using (System.Windows.Forms.FolderBrowserDialog folderBrowser = new System.Windows.Forms.FolderBrowserDialog())
                        {
                            folderBrowser.Description = "请选择解压缩目录";
                            folderBrowser.ShowNewFolderButton = true;
                            folderBrowser.RootFolder = Environment.SpecialFolder.Desktop;
                            if (folderBrowser.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                            ZipFile.ExtractToDirectory(downloadPath, folderBrowser.SelectedPath, true);

                            CVWinSMSConfig.Instance.CVWinSMSPath = folderBrowser.SelectedPath + "\\InstallTool\\CVWinSMS.exe";
                        }

                        // 启动新的实例
                        ProcessStartInfo startInfo = new();
                        startInfo.UseShellExecute = true; // 必须为true才能使用Verb属性
                        startInfo.WorkingDirectory = Environment.CurrentDirectory;
                        startInfo.FileName = CVWinSMSConfig.Instance.CVWinSMSPath;
                        startInfo.Verb = "runas"; // "runas"指定启动程序时请求管理员权限
                                                  // 如果需要静默安装，添加静默安装参数
                                                  //quiet 没法自启，桌面图标也是空                       
                                                  //startInfo.Arguments = "/quiet";

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
        }

        public override void Execute()
        {
            if (!File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath))
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), "找不到管理工具，是否下载", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Task.Run(() => Download());
                    return;
                }

                if (MessageBox.Show(Application.Current.GetActiveWindow(), "I can't find CVWinSMS (CVWinSMS.exe). Would you like to help me find it?", "Open in CVWinSMS", MessageBoxButton.YesNo) == MessageBoxResult.No) return;
                using (System.Windows.Forms.OpenFileDialog openFileDialog = new())
                {
                    openFileDialog.Title = "Select CVWinSMS.exe";
                    openFileDialog.Filter = "CVWinSMS.exe|CVWinSMS.exe";
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    CVWinSMSConfig.Instance.CVWinSMSPath = openFileDialog.FileName;
                }
            }
            try
            {
                Process.Start(CVWinSMSConfig.Instance.CVWinSMSPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
            }
        }

    }
}
