using ColorVision.Common.MVVM;
using log4net;
using System.IO;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// 服务安装视图模型：属性、命令定义、通用辅助方法
    /// 具体实现拆分到 partial 文件:
    ///   - ServiceInstallViewModel.Download.cs  下载逻辑
    ///   - ServiceInstallViewModel.Backup.cs    备份与恢复
    ///   - ServiceInstallViewModel.Install.cs   安装编排
    /// </summary>
    public partial class ServiceInstallViewModel : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ServiceInstallViewModel));
        private int _isCheckingUpdate;

        public ServiceManagerConfig Config => ServiceManagerConfig.Instance;

        public string LogText { get => _logText; set { _logText = value; OnPropertyChanged(); } }
        private string _logText = string.Empty;

        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }
        private bool _isBusy;

        public double Progress { get => _progress; set { _progress = value; OnPropertyChanged(); } }
        private double _progress;

        public string ProgressText { get => _progressText; set { _progressText = value; OnPropertyChanged(); } }
        private string _progressText = string.Empty;

        public string UpdateStatusText { get => _updateStatusText; set { _updateStatusText = value; OnPropertyChanged(); } }
        private string _updateStatusText = "点击更新按钮下载服务包";

        public string ServicePackagePath
        {
            get => _servicePackagePath;
            set
            {
                _servicePackagePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ServicePackageTypeDescription));
            }
        }
        private string _servicePackagePath = string.Empty;

        /// <summary>
        /// 显示已选服务包的类型："增量更新包 (X.X.X.X)" 或 "完整安装包"，未选时为空。
        /// </summary>
        public string ServicePackageTypeDescription
        {
            get
            {
                if (string.IsNullOrEmpty(_servicePackagePath) || !File.Exists(_servicePackagePath))
                    return string.Empty;
                try
                {
                    if (IsIncrementalUpdatePackage(_servicePackagePath))
                    {
                        string ver = System.IO.Path.GetFileNameWithoutExtension(_servicePackagePath);
                        return $"增量更新包 ({ver})";
                    }
                    return "完整安装包";
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public string MySqlPackagePath { get => _mySqlPackagePath; set { _mySqlPackagePath = value; OnPropertyChanged(); } }
        private string _mySqlPackagePath = string.Empty;

        public string MqttInstallerPath { get => _mqttInstallerPath; set { _mqttInstallerPath = value; OnPropertyChanged(); } }
        private string _mqttInstallerPath = string.Empty;

        public bool AutoStartAfterInstall { get => _autoStartAfterInstall; set { _autoStartAfterInstall = value; OnPropertyChanged(); } }
        private bool _autoStartAfterInstall = true;

        public bool AutoUpdateDatabase { get => _autoUpdateDatabase; set { _autoUpdateDatabase = value; OnPropertyChanged(); } }
        private bool _autoUpdateDatabase;

        public bool BackupBeforeInstall { get => _backupBeforeInstall; set { _backupBeforeInstall = value; OnPropertyChanged(); } }
        private bool _backupBeforeInstall;

        public bool BackupServiceBeforeInstall { get => _backupServiceBeforeInstall; set { _backupServiceBeforeInstall = value; OnPropertyChanged(); } }
        private bool _backupServiceBeforeInstall;

        public bool BackupServiceCfgOnly { get => _backupServiceCfgOnly; set { _backupServiceCfgOnly = value; OnPropertyChanged(); } }
        private bool _backupServiceCfgOnly;

        public bool InstallServiceChecked
        {
            get => Config.InstallServiceChecked;
            set { Config.InstallServiceChecked = value; OnPropertyChanged(); }
        }

        public bool InstallMySqlChecked
        {
            get => Config.InstallMySqlChecked;
            set { Config.InstallMySqlChecked = value; OnPropertyChanged(); }
        }

        public bool InstallMqttChecked
        {
            get => Config.InstallMqttChecked;
            set { Config.InstallMqttChecked = value; OnPropertyChanged(); }
        }

        public string DownloadLocation
        {
            get => Config.DownloadLocation;
            set { Config.DownloadLocation = value; OnPropertyChanged(); }
        }

        public RelayCommand CheckUpdateCommand { get; }
        public RelayCommand DownloadMySqlCommand { get; }
        public RelayCommand DownloadMqttCommand { get; }
        public RelayCommand SelectServicePackageCommand { get; }
        public RelayCommand SelectMySqlZipCommand { get; }
        public RelayCommand SelectMqttInstallerCommand { get; }
        public RelayCommand ClearLogCommand { get; }
        public RelayCommand BackupNowCommand { get; }
        public RelayCommand RestoreBackupCommand { get; }
        public RelayCommand BackupServiceNowCommand { get; }
        public RelayCommand RestoreServiceBackupCommand { get; }
        public RelayCommand SelectDownloadLocationCommand { get; }
        public RelayCommand DoInstallCommand { get; }
        public RelayCommand OneKeyInstallAllCommand { get; }

        public ServiceInstallViewModel()
        {
            CheckUpdateCommand = new RelayCommand(a => _ = DownloadLatestServicePackageAsync(), a => !IsBusy);
            DownloadMySqlCommand = new RelayCommand(a => _ = DownloadMySqlAsync(), a => !IsBusy);
            DownloadMqttCommand = new RelayCommand(a => _ = DownloadMqttAsync(), a => !IsBusy);
            SelectServicePackageCommand = new RelayCommand(a => SelectServicePackage());
            SelectMySqlZipCommand = new RelayCommand(a => SelectMySqlZip());
            SelectMqttInstallerCommand = new RelayCommand(a => SelectMqttInstaller());
            ClearLogCommand = new RelayCommand(a => LogText = string.Empty);
            BackupNowCommand = new RelayCommand(a => _ = Task.Run(() => DoBackupNow()), a => !IsBusy);
            RestoreBackupCommand = new RelayCommand(a => _ = Task.Run(() => DoRestoreBackup()), a => !IsBusy);
            BackupServiceNowCommand = new RelayCommand(a => _ = Task.Run(() => DoBackupServiceNow()), a => !IsBusy);
            RestoreServiceBackupCommand = new RelayCommand(a => _ = Task.Run(() => DoRestoreServiceBackup()), a => !IsBusy);
            SelectDownloadLocationCommand = new RelayCommand(a => SelectDownloadLocation());
            DoInstallCommand = new RelayCommand(a => _ = ExecuteInstallAsync(), a => !IsBusy);
            OneKeyInstallAllCommand = new RelayCommand(a => _ = OneKeyInstallAllAsync(), a => !IsBusy);
        }

        #region File Dialogs

        private void SelectServicePackage()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "服务安装包 (*.zip)|*.zip",
                Title = "选择服务安装包"
            };

            if (dlg.ShowDialog() == true)
                ServicePackagePath = dlg.FileName;
        }

        private void SelectMySqlZip()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MySQL ZIP (*.zip)|*.zip",
                Title = "选择 MySQL 安装包"
            };

            if (dlg.ShowDialog() == true)
                MySqlPackagePath = dlg.FileName;
        }

        private void SelectMqttInstaller()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MQTT 安装程序 (*.exe)|*.exe",
                Title = "选择 MQTT 安装程序"
            };

            if (dlg.ShowDialog() == true)
                MqttInstallerPath = dlg.FileName;
        }

        private void SelectDownloadLocation()
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择下载保存目录",
                ShowNewFolderButton = true
            };
            if (!string.IsNullOrWhiteSpace(DownloadLocation) && Directory.Exists(DownloadLocation))
                dlg.SelectedPath = DownloadLocation;

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DownloadLocation = dlg.SelectedPath;
                AddLog($"下载目录已设置为: {DownloadLocation}");
            }
        }

        #endregion

        #region Shared Helpers

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Application.Current?.Dispatcher.Invoke(() =>
            {
                LogText += $"[{timestamp}] {message}\n";
            });
        }

        private void SetBusy(bool busy, string text = "")
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsBusy = busy;
                ProgressText = text;
                if (!busy)
                {
                    Progress = 0;
                }
            });
        }

        private void SetProgress(double value, string text)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Progress = value;
                ProgressText = text;
            });
            AddLog(text);
        }

        #endregion
    }
}
