using ColorVision.Common.MVVM;
using ColorVision.UI;
using log4net;
using System.IO;
using System.Windows;
using WindowsServicePlugin.Properties;

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

        public string Vc2013InstallerPath { get => _vc2013InstallerPath; set { _vc2013InstallerPath = value; OnPropertyChanged(); } }
        private string _vc2013InstallerPath = string.Empty;

        public bool IsMySqlInstallVisible { get => _isMySqlInstallVisible; set { _isMySqlInstallVisible = value; OnPropertyChanged(); } }
        private bool _isMySqlInstallVisible = true;

        public bool IsMqttInstallVisible { get => _isMqttInstallVisible; set { _isMqttInstallVisible = value; OnPropertyChanged(); } }
        private bool _isMqttInstallVisible = true;

        public bool IsVc2013InstallVisible { get => _isVc2013InstallVisible; set { _isVc2013InstallVisible = value; OnPropertyChanged(); } }
        private bool _isVc2013InstallVisible = true;

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

        public bool InstallVc2013Checked { get => _installVc2013Checked; set { _installVc2013Checked = value; OnPropertyChanged(); } }
        private bool _installVc2013Checked;

        public RelayCommand SelectServicePackageCommand { get; }
        public RelayCommand SelectMySqlZipCommand { get; }
        public RelayCommand SelectMqttInstallerCommand { get; }
        public RelayCommand SelectVc2013InstallerCommand { get; }
        public RelayCommand BackupNowCommand { get; }
        public RelayCommand RestoreBackupCommand { get; }
        public RelayCommand BackupServiceNowCommand { get; }
        public RelayCommand RestoreServiceBackupCommand { get; }
        public RelayCommand DoInstallCommand { get; }
        public RelayCommand OneKeyInstallAllCommand { get; }
        public RelayCommand DownloadOneKeyPackageCommand { get; }
        public RelayCommand DownloadServicePackageCommand { get; }
        public RelayCommand DownloadMySqlPackageCommand { get; }
        public RelayCommand DownloadMqttInstallerCommand { get; }
        public RelayCommand DownloadVc2013InstallerCommand { get; }
        public RelayCommand OnlineDownloadCommand { get; }
        public RelayCommand SelectBaseLocationCommand { get; }

        public ServiceInstallViewModel()
        {
            SelectServicePackageCommand = new RelayCommand(a => SelectServicePackage());
            SelectMySqlZipCommand = new RelayCommand(a => SelectMySqlZip());
            SelectMqttInstallerCommand = new RelayCommand(a => SelectMqttInstaller());
            SelectVc2013InstallerCommand = new RelayCommand(a => SelectVc2013Installer());
            BackupNowCommand = new RelayCommand(a => _ = Task.Run(() => DoBackupNow()), a => !IsBusy);
            RestoreBackupCommand = new RelayCommand(a => _ = Task.Run(() => DoRestoreBackup()), a => !IsBusy);
            BackupServiceNowCommand = new RelayCommand(a => _ = Task.Run(() => DoBackupServiceNow()), a => !IsBusy);
            RestoreServiceBackupCommand = new RelayCommand(a => _ = Task.Run(() => DoRestoreServiceBackup()), a => !IsBusy);
            DoInstallCommand = new RelayCommand(a => _ = ExecuteInstallAsync(), a => !IsBusy);
            OneKeyInstallAllCommand = new RelayCommand(a => _ = OneKeyInstallAllAsync(), a => !IsBusy);
            DownloadServicePackageCommand = new RelayCommand(a => _ = DownloadServicePackageAsync(), a => !IsBusy);
            DownloadOneKeyPackageCommand = DownloadServicePackageCommand;
            DownloadMySqlPackageCommand = new RelayCommand(a => _ = DownloadMySqlPackageAsync(), a => !IsBusy);
            DownloadMqttInstallerCommand = new RelayCommand(a => _ = DownloadMqttInstallerAsync(), a => !IsBusy);
            DownloadVc2013InstallerCommand = new RelayCommand(a => _ = DownloadVc2013InstallerAsync(), a => !IsBusy);
            OnlineDownloadCommand = new RelayCommand(a => _ = OnlineDownloadAsync(), a => !IsBusy);
            SelectBaseLocationCommand = new RelayCommand(a => SelectBaseLocation(), a => !IsBusy);
            RefreshInstallComponentState();
        }

        #region File Dialogs

        private void SelectBaseLocation()
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = Resources.SelectServiceBaseLocationDialog,
                SelectedPath = Directory.Exists(Config.BaseLocation) ? Config.BaseLocation : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                ShowNewFolderButton = true
            };

            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            Config.BaseLocation = dlg.SelectedPath;
            ConfigHandler.GetInstance().Save<ServiceManagerConfig>();
            OnPropertyChanged(nameof(Config));
        }

        private void SelectServicePackage()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = Resources.ServicePackageZipFilter,
                Title = Resources.SelectServicePackageDialog
            };

            if (dlg.ShowDialog() == true)
                ServicePackagePath = dlg.FileName;
        }

        private void SelectMySqlZip()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = Resources.MySqlZipFilter,
                Title = Resources.SelectMySqlPackageDialog
            };

            if (dlg.ShowDialog() == true)
                MySqlPackagePath = dlg.FileName;
        }

        private void SelectMqttInstaller()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = Resources.MqttInstallerFilter,
                Title = Resources.SelectMqttInstallerDialog
            };

            if (dlg.ShowDialog() == true)
                MqttInstallerPath = dlg.FileName;
        }

        private void SelectVc2013Installer()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = Resources.Vc2013InstallerFilter,
                Title = Resources.SelectVc2013InstallerDialog
            };

            if (dlg.ShowDialog() == true)
            {
                Vc2013InstallerPath = dlg.FileName;
                InstallVc2013Checked = true;
            }
        }

        #endregion

        private void RefreshInstallComponentState()
        {
            var manager = ServiceManagerViewModel.Instance;
            manager.RefreshAll();
            IsMySqlInstallVisible = !manager.MySqlManager.Config.IsInstalled;
            IsMqttInstallVisible = !manager.MqttManager.Config.IsInstalled;
            IsVc2013InstallVisible = !IsVc2013RuntimeInstalled();
            if (!IsMySqlInstallVisible) InstallMySqlChecked = false;
            if (!IsMqttInstallVisible) InstallMqttChecked = false;
            if (!IsVc2013InstallVisible) InstallVc2013Checked = false;
        }

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
