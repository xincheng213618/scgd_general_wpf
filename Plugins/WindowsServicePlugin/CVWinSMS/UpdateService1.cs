using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;


namespace WindowsServicePlugin.CVWinSMS
{
    public class UpdateService1 : ViewModelBase
    {
        public static UpdateService1 Instance { get; set; } = new UpdateService1();
        public DownloadFile DownloadFile { get; set; } = new DownloadFile();

        public UpdateService1()
        {
            DownloadFile = new DownloadFile();
            DownloadFile.DownloadTile = "下载最新的服务压缩包";
            if (FindPath("RegistrationCenterService"))
            {
                
            }
            Task.Run(Initialize);
        }
        public int StepIndex { get => _StepIndex; set { _StepIndex = value; NotifyPropertyChanged(); } }
        private int _StepIndex = 0;


        public static string LatestReleaseUrl => "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/CVWindowsService/LATEST_RELEASE";

        private string url;

        public string DownloadPath { get => _downloadPath; set { _downloadPath = value; NotifyPropertyChanged(); } }

        private string _downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + @"ColorVision\\";

        public Version CurrentVerision { get => _CurrentVerision; set { _CurrentVerision = value; NotifyPropertyChanged(); } }
        private Version _CurrentVerision = new Version();

        public Version Verision { get => _Verision; set { _Verision = value; NotifyPropertyChanged(); } }
        private Version _Verision = new Version();

        public string RegistrationCenterService { get => _RegistrationCenterService; set { _RegistrationCenterService = value; NotifyPropertyChanged(); } }
        private string _RegistrationCenterService = string.Empty;

        public string? InstallPath { get => _InstallPath; set { _InstallPath = value; NotifyPropertyChanged(); } }
        private string? _InstallPath = string.Empty;


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
                        RegistrationCenterService = Environment.ExpandEnvironmentVariables(str);
                        InstallPath = Directory.GetParent(Directory.GetParent(RegistrationCenterService)?.FullName)?.FullName;

                        if (File.Exists(RegistrationCenterService))
                        {
                            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(RegistrationCenterService);
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
            Execute();

        }



        public async void Execute()
        {
            Verision = await DownloadFile.GetLatestVersionNumber(LatestReleaseUrl);
            if (Verision > CurrentVerision)
            {
                string filepath = $"CVWindowsService[{Verision}]-{Verision.Revision:D4}.zip";

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (MessageBox.Show(Application.Current.GetActiveWindow(), $"服务{CurrentVerision}:找到新版本{filepath}，是否更新", $"{CurrentVerision}", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        DownloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + $"ColorVision\\{filepath}";
                        string url = $"http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/CVWindowsService/{filepath}";
                        WindowUpdate windowUpdate = new WindowUpdate(DownloadFile) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                        if (!File.Exists(DownloadPath))
                        {
                            windowUpdate.Show();
                        }
                        Task.Run(async () =>
                        {
                            if (!File.Exists(DownloadPath))
                            {
                                await DownloadFile.GetIsPassWorld();
                                CancellationTokenSource _cancellationTokenSource = new();
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    windowUpdate.Show();
                                });
                                await DownloadFile.Download(url, DownloadPath, _cancellationTokenSource.Token);
                            }
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                windowUpdate.Close();
                            });
                            StepIndex = 1;
                            try
                            {
                                PlatformHelper.OpenFolderAndSelectFile(DownloadPath);
                                Application.Current.Dispatcher.Invoke(() =>
                                {
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
                                            StepIndex = 2;
                                            Tool.ExecuteCommandAsAdmin("net stop RegistrationCenterService");
                                            if (Directory.Exists(InstallPath))
                                            {
                                                if (MessageBox.Show(Application.Current.GetActiveWindow(), "全新安装需要源目录不存在文件，是否删除安装目录下的文件", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                                                {
                                                    Directory.Delete(InstallPath, true);
                                                }
                                            }
                                            ColorVision.Common.NativeMethods.Clipboard.SetText(DownloadPath);
                                            new InstallTool().Execute();
                                            StepIndex = 3;
                                            MessageBox.Show(Application.Current.GetActiveWindow(), "更新完成后请点击恢复数据库");
                                            new ExportMySqlTool().Execute();
                                        });
      
                                    });

                                });


                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.ToString());
                                File.Delete(DownloadPath);
                            }
                        });

                    }
                });

            }




        }

    }



}
