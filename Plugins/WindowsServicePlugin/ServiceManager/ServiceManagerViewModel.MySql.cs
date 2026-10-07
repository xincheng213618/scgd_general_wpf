using System.Diagnostics;
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
                if (!await CheckMySqlRuntimeAsync(dlg.FileName))
                    return;

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
                if (!MySqlManager.ResolveSavedMySqlBasePath(log.Info)
                    || !await CheckMySqlRuntimeAsync(MySqlManager.Helper.MysqldExePath))
                    return;

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

        private async Task<bool> CheckMySqlRuntimeAsync(string mysqlPath)
        {
            string? message = await Task.Run(() => MySqlRuntimePrerequisite.GetValidationMessage(
                MySqlRuntimePrerequisite.ReadVersion(mysqlPath), MySqlRuntimePrerequisite.IsVc2013Installed()));
            if (message == null)
                return true;

            log.Info(message);
            ShowUiMessage(message, Properties.Resources.MySqlPrerequisiteTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
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

            if (string.IsNullOrWhiteSpace(MySqlManager.Config.Database))
            {
                ShowUiMessage("请先填写目标数据库名称。", "重置数据库", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string host = string.IsNullOrWhiteSpace(MySqlManager.Config.Host) ? "127.0.0.1" : MySqlManager.Config.Host.Trim();
            var confirm = ShowUiMessage(
                $"将清除 {host}:{MySqlManager.GetConfiguredPort(Config.MySqlPort)} 上的数据库 {MySqlManager.Config.Database.Trim()}，再执行安装 SQL：\n{sqlFilePath}\n\n现有表和数据会被删除，旧流程、模板和资源不会回写。此操作不能撤销，是否继续？",
                "重置数据库",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
                return;

            bool resetSucceeded = false;
            SetBusy(true, "正在重置数据库...");
            try
            {
                bool ok = await Task.Run(() => MySqlManager.ResetDatabaseFromServiceSql(log.Info));
                if (ok)
                {
                    log.Info("数据库重置完成");
                    SyncManagedServiceConfigs();
                    SyncLegacyAppConfig();
                    resetSucceeded = true;
                }
                else
                {
                    log.Info("数据库重置失败");
                    ShowUiMessage("数据库重置未完成，请查看服务日志确认目标库当前状态。", "重置数据库", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                log.Error("数据库重置后的服务配置同步失败；请核对数据库和服务配置状态", ex);
                ShowUiMessage("数据库可能已重建，但服务配置同步失败。请查看服务日志并核对目标库与服务配置。", "重置数据库", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
                RefreshAll();
            }

            if (resetSucceeded)
                await PromptForRestartsAfterResetAsync();
        }

        private async Task PromptForRestartsAfterResetAsync()
        {
            bool serviceRestartFailed = false;
            if (ShowUiMessage("数据库已按安装 SQL 重建。是否重启注册中心服务以加载新数据库？",
                    "重置数据库", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                SetBusy(true, "正在重启注册中心服务...");
                try
                {
                    bool restarted = await ServiceHostWindowsServiceController.ExecuteAsync(
                        "RegistrationCenterService", ServiceHostServiceOperation.Restart, log.Info, "注册中心服务");
                    serviceRestartFailed = !restarted;
                    if (!restarted)
                        ShowUiMessage("注册中心服务重启失败，请查看服务日志并检查实际服务状态。",
                            "重置数据库", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    serviceRestartFailed = true;
                    log.Error("注册中心服务重启失败", ex);
                    ShowUiMessage("注册中心服务重启失败，请查看服务日志并检查实际服务状态。",
                        "重置数据库", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    SetBusy(false);
                    RefreshAll();
                }
            }

            string restartPrompt = serviceRestartFailed
                ? "注册中心服务重启未成功。是否仍要重启 ColorVision 软件？"
                : "是否重启 ColorVision 软件以加载新数据库？";
            if (ShowUiMessage(restartPrompt, "重置数据库", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                string applicationPath = Path.ChangeExtension(Application.ResourceAssembly.Location, ".exe");
                Process? process = Process.Start(applicationPath, "-r");
                if (process == null)
                    throw new InvalidOperationException("未能创建新的应用进程。");
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                log.Error("ColorVision 重启失败", ex);
                ShowUiMessage($"ColorVision 重启失败：{ex.Message}", "重置数据库", MessageBoxButton.OK, MessageBoxImage.Error);
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
