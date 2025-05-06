using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using WindowsServicePlugin.CVWinSMS;

namespace WindowsServicePlugin.Serv
{
    public class InstallService :  WizardStepBase, IMainWindowInitialized
    {
        public override int Order => 99;
        public override string Header => "下载服务";

        public override string Description => "下载最近的服务压缩包，安装服务时请先解压到需要安装的位置在下载服务管理工具，配置路径到该位置";

        public DownloadFile DownloadFile { get; set; } = new DownloadFile();
        public InstallService()
        {
            DownloadFile = new DownloadFile();
            DownloadFile.DownloadTile = "下载最新的服务压缩包";
            if (FindPath("RegistrationCenterService"))
            {
                
            }
        }

        public static string LatestReleaseUrl => "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/CVWindowsService/LATEST_RELEASE";

        private string url = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/CVWindowsService/CVWindowsService%5B2.7.0.402%5D-0402.zip";

        private string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + @"ColorVision\\";

        public Version CurrentVerision { get; set; } = new Version();

        bool FindPath(string serviceName)
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}"))
            {
                if (key != null)
                {
                    object imagePath = key.GetValue("ImagePath");
                    if (imagePath is string str)
                    {
                        // 去除引号
                        str = str.Trim('"');

                        // 处理环境变量
                        string expandedPath = Environment.ExpandEnvironmentVariables(str);

                        if (File.Exists(expandedPath))
                        {
                            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(expandedPath);
                            CurrentVerision = new Version(versionInfo.FileVersion);
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        public async Task Initialize()
        {
            // 如果是调试模式，不进行更新检测
            //if (Debugger.IsAttached) return;

            if (CVWinSMSConfig.Instance.IsAutoUpdate)
            {
                Execute();
            }
        }



        public override async void Execute()
        {
            Version version = await DownloadFile.GetLatestVersionNumber(LatestReleaseUrl);
            if (version > CurrentVerision)
            {
                string filepath = $"CVWindowsService[{version}]-{version.Revision:D4}.zip";

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (MessageBox.Show(Application.Current.GetActiveWindow(), $"服务{CurrentVerision}:找到新版本{filepath}，是否更新", $"{CurrentVerision}", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + $"ColorVision\\{filepath}";
                        string url = $"http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/CVWindowsService/{filepath}";
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

                            try
                            {
                                PlatformHelper.OpenFolderAndSelectFile(downloadPath);
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    MessageBox.Show(Application.Current.GetActiveWindow(), "更新前需要先备份数据库","3.0更新助手");
                                    string sql = "ALTER TABLE `t_scgd_algorithm_result_master`\r\nADD COLUMN `version` varchar(16) DEFAULT NULL COMMENT '版本号' AFTER `img_file_type`;";
                                    MySqlControl.GetInstance().ExecuteNonQuery(sql);
                                    string sql1 = "ALTER TABLE `t_scgd_sys_dictionary_mod_master`\r\nADD COLUMN `version` varchar(16) DEFAULT NULL COMMENT '版本号' AFTER `cfg_json`;";
                                    MySqlControl.GetInstance().ExecuteNonQuery(sql1);
                                    var mySqlLocalServices = MySqlLocalServicesManager.GetInstance();
                                    Task.Run(() =>
                                    {
                                        mySqlLocalServices.BackupMysqlResource();
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            MessageBox.Show(Application.Current.GetActiveWindow(), "数据库备份完成，接下来请点击更新按钮");
                                            new InstallTool().Execute();
                                            MessageBox.Show(Application.Current.GetActiveWindow(), "更新完成后请点击恢复数据库");
                                            new ExportMySqlTool().Execute();
                                        });
      
                                    });

                                });


                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.ToString());
                                File.Delete(downloadPath);
                            }
                        });

                    }
                });

            }




        }

    }



}
