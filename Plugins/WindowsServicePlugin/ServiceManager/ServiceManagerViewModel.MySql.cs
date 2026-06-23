using System.IO;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// MySQL 操作：安装、脚本执行、密码管理、用户管理
    /// </summary>
    public partial class ServiceManagerViewModel
    {
        private async Task MySqlInstallZipAsync()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MySQL ZIP (*.zip)|*.zip",
                Title = "选择 mysql-5.7.37-winx64.zip"
            };
            if (dlg.ShowDialog() != true) return;

            string basePath = Config.BaseLocation;
            if (string.IsNullOrEmpty(basePath))
            {
                MessageBox.Show("请先设置安装根目录", "MySQL安装", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetBusy(true, "正在安装 MySQL...");
            try
            {
                bool result = await MySqlManager.InstallFromZipViaServiceHostAsync(dlg.FileName, basePath, log.Info);
                if (result)
                {
                    log.Info("MySQL 安装成功");
                    SyncAllConfigs(false);
                    RefreshAll();
                }
                else
                {
                    log.Info("MySQL 安装失败");
                }
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task RegisterExistingMySqlServiceAsync()
        {
            SetBusy(true, "正在通过后台服务注册 MySQL 服务...");
            try
            {
                bool ok = await MySqlManager.RegisterExistingServiceViaServiceHostAsync(log.Info).ConfigureAwait(true);
                if (ok)
                {
                    log.Info("MySQL 服务注册完成");
                    SyncLegacyAppConfig();
                }
                else
                {
                    log.Info("MySQL 服务注册失败");
                }
            }
            finally
            {
                SetBusy(false);
                RefreshAll();
            }
        }

        private async Task StartMySqlAsync()
        {
            SetBusy(true, "正在通过后台服务启动 MySQL...");
            try
            {
                bool ok = await MySqlManager.StartViaServiceHostAsync(log.Info).ConfigureAwait(true);
                log.Info(ok ? "MySQL 服务启动完成" : "MySQL 服务启动失败");
            }
            finally
            {
                SetBusy(false);
                RefreshAll();
            }
        }

        private async Task StopMySqlAsync()
        {
            SetBusy(true, "正在通过后台服务停止 MySQL...");
            try
            {
                bool ok = await MySqlManager.StopViaServiceHostAsync(log.Info).ConfigureAwait(true);
                log.Info(ok ? "MySQL 服务停止完成" : "MySQL 服务停止失败");
            }
            finally
            {
                SetBusy(false);
                RefreshAll();
            }
        }

        private async Task UninstallMySqlAsync()
        {
            SetBusy(true, "正在通过后台服务卸载 MySQL...");
            try
            {
                bool ok = await MySqlManager.UninstallViaServiceHostAsync(log.Info).ConfigureAwait(true);
                log.Info(ok ? "MySQL 服务卸载完成" : "MySQL 服务卸载失败");
            }
            finally
            {
                SetBusy(false);
                RefreshAll();
            }
        }

        private async Task RunSqlScriptAsync()
        {
            string filePath = MySqlManager.Config.SqlScriptPath;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                ShowUiMessage("请先选择或输入 SQL 脚本路径。", "脚本执行", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!File.Exists(filePath))
            {
                ShowUiMessage($"SQL 脚本不存在：\n{filePath}", "脚本执行", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MySqlManager.SetSqlScriptPath(filePath);
            SetBusy(true, "正在执行 SQL 脚本...");
            try
            {
                bool ok = await Task.Run(() => MySqlManager.ExecuteSqlFile(filePath, log.Info));
                log.Info(ok ? "SQL 脚本执行完成" : "SQL 脚本执行失败");
            }
            finally
            {
                SetBusy(false);
                RefreshAll();
            }
        }

        private async Task ResetDatabaseAsync()
        {
            if (string.IsNullOrWhiteSpace(MySqlManager.Config.RootPassword))
            {
                ShowUiMessage("请先填写 root 密码。", "重置数据库", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string? sqlFilePath = MySqlServiceManager.ResolveResetDatabaseSqlPath();
            if (string.IsNullOrWhiteSpace(sqlFilePath))
            {
                ShowUiMessage("未找到 color_vision_all.sql，请确认服务安装目录下存在 SQL 目录。", "重置数据库", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = ShowUiMessage(
                $"将使用 root 账号执行数据库重置脚本：\n{sqlFilePath}\n\n该脚本会重建/覆盖部分数据库表，是否继续？",
                "重置数据库",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
                return;

            SetBusy(true, "正在重置数据库...");
            try
            {
                bool ok = await Task.Run(() => MySqlManager.ResetDatabaseFromServiceSql(log.Info));
                if (ok)
                {
                    log.Info("数据库重置完成");
                    SyncManagedServiceConfigs();
                    SyncLegacyAppConfig();
                }
                else
                {
                    log.Info("数据库重置失败");
                }
            }
            finally
            {
                SetBusy(false);
                RefreshAll();
            }
        }

        private void DoApplyRootPassword()
        {
            if (MySqlManager.ApplyRootPassword(log.Info))
            {
                SyncLegacyAppConfig();
                RefreshMySqlStatus();
            }
        }

        private void DoCreateOrUpdateUser()
        {
            if (MySqlManager.CreateOrUpdateUser(log.Info))
            {
                SyncLegacyAppConfig();
                SyncAllConfigs(false);
                log.Info("业务用户配置已更新到 MySqlServiceConfig");
            }
        }

        private void GenerateRandomRootPassword()
        {
            MySqlManager.GenerateRandomRootPassword(log.Info);
        }

        private void BrowseSqlScriptPath()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "SQL 文件 (*.sql)|*.sql",
                Title = "选择 SQL 脚本",
                FileName = MySqlManager.Config.SqlScriptPath
            };
            if (dlg.ShowDialog() == true)
            {
                MySqlManager.SetSqlScriptPath(dlg.FileName);
            }
        }

        private void BrowseMySqlPath()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "mysqld.exe|mysqld.exe",
                Title = "选择 mysqld.exe"
            };
            if (dlg.ShowDialog() == true)
            {
                MySqlManager.SetManualBasePath(dlg.FileName);
                RefreshMySqlStatus();
            }
        }

        private static MessageBoxResult ShowUiMessage(string message, string caption, MessageBoxButton button, MessageBoxImage image)
        {
            var application = Application.Current;
            if (application == null)
                return MessageBox.Show(message, caption, button, image);

            var dispatcher = application.Dispatcher;
            return dispatcher.CheckAccess() ? ShowUiMessageCore(application, message, caption, button, image) : dispatcher.Invoke(() => ShowUiMessageCore(application, message, caption, button, image));
        }

        private static MessageBoxResult ShowUiMessageCore(Application application, string message, string caption, MessageBoxButton button, MessageBoxImage image)
        {
            var owner = application.GetActiveWindow();
            return owner == null ? MessageBox.Show(message, caption, button, image) : MessageBox.Show(owner, message, caption, button, image);
        }
    }
}
