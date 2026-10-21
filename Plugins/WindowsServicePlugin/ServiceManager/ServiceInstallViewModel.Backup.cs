using System.IO;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// 数据库手动备份与恢复
    /// </summary>
    public partial class ServiceInstallViewModel
    {
        private void DoBackupNow()
        {
            try
            {
                var mySqlManager = ServiceManagerViewModel.Instance.MySqlManager;
                mySqlManager.RefreshStatus(ServiceManagerViewModel.Instance.Services, ServiceManagerViewModel.Instance.Config.MySqlPort);
                if (!mySqlManager.Config.IsRunning)
                {
                    log.Info("MySQL 未运行，跳过备份");
                    return;
                }

                mySqlManager.BackupDatabase(mySqlManager.Config.Database, log.Info);
            }
            catch (Exception ex)
            {
                log.Info($"备份失败: {ex.Message}");
            }
        }

        private void DoRestoreBackup()
        {
            string? filePath = null;
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "SQL 备份文件 (*.sql)|*.sql",
                    Title = "选择备份文件",
                    InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "Backup")
                };
                if (dlg.ShowDialog() == true)
                    filePath = dlg.FileName;
            });

            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                var mySqlManager = ServiceManagerViewModel.Instance.MySqlManager;
                mySqlManager.RefreshStatus(ServiceManagerViewModel.Instance.Services, ServiceManagerViewModel.Instance.Config.MySqlPort);
                if (!mySqlManager.Config.IsRunning)
                {
                    log.Info("MySQL 未运行，无法恢复");
                    return;
                }

                mySqlManager.RestoreDatabase(filePath, log.Info);
            }
            catch (Exception ex)
            {
                log.Info($"恢复失败: {ex.Message}");
            }
        }
    }
}
