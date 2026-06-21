using ColorVision.Common.MVVM;
using log4net;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// 服务安装视图模型：属性、命令定义、通用辅助方法
    /// 具体实现拆分到 partial 文件:
    ///   - ServiceInstallViewModel.Backup.cs    备份与恢复
    ///   - ServiceInstallViewModel.Install.cs   安装编排
    /// </summary>
    public partial class ServiceInstallViewModel : ViewModelBase
    {
        private readonly ILog log = LogManager.GetLogger(typeof(ServiceInstallViewModel));
        private readonly ServiceManagerConfig _config = ServiceManagerConfig.Instance;
        public ServiceManagerConfig Config => _config;

        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }
        private bool _isBusy;

        public double Progress { get => _progress; set { _progress = value; OnPropertyChanged(); } }
        private double _progress;

        public string ProgressText { get => _progressText; set { _progressText = value; OnPropertyChanged(); } }
        private string _progressText = string.Empty;

        public string ServicePackagePath
        {
            get => _servicePackagePath;
            set
            {
                _servicePackagePath = value;
                OnPropertyChanged();
            }
        }
        private string _servicePackagePath = string.Empty;

        public string MySqlPackagePath { get => _mySqlPackagePath; set { _mySqlPackagePath = value; OnPropertyChanged(); } }
        private string _mySqlPackagePath = string.Empty;

        public string MqttInstallerPath { get => _mqttInstallerPath; set { _mqttInstallerPath = value; OnPropertyChanged(); } }
        private string _mqttInstallerPath = string.Empty;

        public bool AutoUpdateDatabase { get => _autoUpdateDatabase; set { _autoUpdateDatabase = value; OnPropertyChanged(); } }
        private bool _autoUpdateDatabase;

        public bool BackupBeforeInstall { get => _backupBeforeInstall; set { _backupBeforeInstall = value; OnPropertyChanged(); } }
        private bool _backupBeforeInstall;

        public bool BackupServiceBeforeInstall { get => _backupServiceBeforeInstall; set { _backupServiceBeforeInstall = value; OnPropertyChanged(); } }
        private bool _backupServiceBeforeInstall;

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

        public RelayCommand SelectServicePackageCommand { get; }
        public RelayCommand SelectMySqlZipCommand { get; }
        public RelayCommand SelectMqttInstallerCommand { get; }
        public RelayCommand BackupNowCommand { get; }
        public RelayCommand RestoreBackupCommand { get; }
        public RelayCommand BackupServiceNowCommand { get; }
        public RelayCommand RestoreServiceBackupCommand { get; }
        public RelayCommand DoInstallCommand { get; }
        public RelayCommand OneKeyInstallAllCommand { get; }
        public RelayCommand DownloadOneKeyPackageCommand { get; }

        public ServiceInstallViewModel()
        {
            SelectServicePackageCommand = new RelayCommand(a => SelectServicePackage());
            SelectMySqlZipCommand = new RelayCommand(a => SelectMySqlZip());
            SelectMqttInstallerCommand = new RelayCommand(a => SelectMqttInstaller());
            BackupNowCommand = new RelayCommand(a => _ = Task.Run(() => DoBackupNow()), a => !IsBusy);
            RestoreBackupCommand = new RelayCommand(a => _ = Task.Run(() => DoRestoreBackup()), a => !IsBusy);
            BackupServiceNowCommand = new RelayCommand(a => _ = Task.Run(() => DoBackupServiceNow()), a => !IsBusy);
            RestoreServiceBackupCommand = new RelayCommand(a => _ = Task.Run(() => DoRestoreServiceBackup()), a => !IsBusy);
            DoInstallCommand = new RelayCommand(a => _ = ExecuteInstallAsync(), a => !IsBusy);
            OneKeyInstallAllCommand = new RelayCommand(a => _ = OneKeyInstallAllAsync(), a => !IsBusy);
            DownloadOneKeyPackageCommand = new RelayCommand(a => _ = DownloadOneKeyPackageAsync(), a => !IsBusy);
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

        #endregion

        #region Shared Helpers

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
            log.Info(text);
        }

        #endregion
    }
}
