using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// MySQL 操作：安装、备份、恢复、密码管理、用户管理
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

        private void DoMySqlBackup()
        {
            MySqlManager.BackupDatabase(log.Info);
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

        private async Task RepairMySqlServicePreferServiceHostAsync()
        {
            SetBusy(true, "正在通过后台服务修复 MySQL...");
            try
            {
                bool ok = await MySqlManager.RepairOrRestartViaServiceHostAsync(log.Info).ConfigureAwait(true);
                if (ok)
                {
                    log.Info("MySQL 后台修复/重启完成");
                    SyncLegacyAppConfig();
                }
                else
                {
                    log.Info("MySQL 后台修复/重启失败");
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

        private void DoMySqlRestore()
        {
            string? filePath = null;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "SQL 文件 (*.sql)|*.sql",
                    Title = "选择备份文件"
                };
                if (dlg.ShowDialog() == true)
                    filePath = dlg.FileName;
            });

            if (string.IsNullOrEmpty(filePath)) return;

            MySqlManager.RestoreDatabase(filePath, log.Info);
        }

        private void DoRunSqlScript()
        {
            string? filePath = null;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "SQL 文件 (*.sql)|*.sql",
                    Title = "选择 SQL 脚本"
                };
                if (dlg.ShowDialog() == true)
                    filePath = dlg.FileName;
            });

            if (string.IsNullOrEmpty(filePath)) return;

            MySqlManager.ExecuteSqlFile(filePath, log.Info);
        }

        private async Task ResetDatabaseAsync()
        {
            if (string.IsNullOrWhiteSpace(MySqlManager.Config.RootPassword))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "请先填写 root 密码。", "重置数据库", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string? sqlFilePath = MySqlServiceManager.ResolveResetDatabaseSqlPath();
            if (string.IsNullOrWhiteSpace(sqlFilePath))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "未找到 color_vision_all.sql，请确认服务安装目录下存在 SQL 目录。", "重置数据库", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                Application.Current.GetActiveWindow(),
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

        private void DoSetRootPassword()
        {
            if (MySqlManager.SetRootPassword(log.Info))
            {
                SyncLegacyAppConfig();
                RefreshMySqlStatus();
            }
        }

        private void DoForceResetRootPassword()
        {
            if (MySqlManager.ForceResetRootPassword(log.Info))
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

        private void DoCheckDatabaseConfig()
        {
            SetBusy(true, "正在检查数据库配置...");
            try
            {
                var current = MySqlManager.Config;
                var legacy = ReadLegacyMySqlProfile();

                bool currentAppOk = MySqlManager.TestConnection(current.Host, current.Port, current.AppUser, current.AppPassword, current.Database);
                bool currentRootOk = !string.IsNullOrWhiteSpace(current.RootPassword)
                    && MySqlManager.TestConnection(current.Host, current.Port, "root", current.RootPassword, null);

                bool legacyAppOk = legacy != null
                    && !string.IsNullOrWhiteSpace(legacy.AppUser)
                    && MySqlManager.TestConnection(legacy.Host, legacy.Port, legacy.AppUser, legacy.AppPassword, legacy.Database);
                bool legacyRootOk = legacy != null
                    && !string.IsNullOrWhiteSpace(legacy.RootPassword)
                    && MySqlManager.TestConnection(legacy.Host, legacy.Port, "root", legacy.RootPassword, null);

                log.Info($"当前配置业务账号校验: {(currentAppOk ? "成功" : "失败")}");
                log.Info($"旧版配置业务账号校验: {(legacyAppOk ? "成功" : "失败")}");

                if (currentAppOk && !legacyAppOk)
                {
                    SyncManagedServiceConfigs();
                    if (legacy != null)
                    {
                        SyncLegacyMySqlProfileSafely(CreateLegacyProfileFromCurrent(), true);
                        MessageBox.Show(Application.Current.GetActiveWindow(),"当前服务管理器中的数据库配置可用，已同步旧版配置。", "数据库配置检查", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "当前服务管理器中的数据库配置可用，且未检测到旧版配置文件。", "数据库配置检查", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    RefreshAll();
                    return;
                }

                if (!currentAppOk && legacyAppOk && legacy != null)
                {
                    string resolvedRootPassword = string.IsNullOrWhiteSpace(legacy.RootPassword) ? current.RootPassword : legacy.RootPassword;
                    MySqlManager.UpdateStoredCredentials(legacy.Host, legacy.Port, resolvedRootPassword, legacy.AppUser, legacy.AppPassword, legacy.Database);
                    SyncManagedServiceConfigs();
                    RefreshAll();
                    MessageBox.Show(Application.Current.GetActiveWindow(), "旧版 App.config 中的数据库配置可用，已同步当前服务管理器配置。", "数据库配置检查", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (currentAppOk && legacyAppOk)
                {
                    string message = legacy != null &&
                        current.AppUser == legacy.AppUser &&
                        current.AppPassword == legacy.AppPassword &&
                        current.Database == legacy.Database
                        ? "当前配置和旧版配置都可用，且账号信息一致。"
                        : "当前配置和旧版配置都可用，但账号信息并不完全一致，未自动覆盖。";
                    MessageBox.Show(Application.Current.GetActiveWindow(), message, "数据库配置检查", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (MessageBox.Show(Application.Current.GetActiveWindow(), "当前配置和旧版配置的业务账号都无法连接数据库，是否尝试重置业务账号并同步配置？", "数据库配置检查", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }

                if (!currentRootOk && legacyRootOk && legacy != null)
                {
                    MySqlManager.UpdateStoredCredentials(current.Host, current.Port, legacy.RootPassword, current.AppUser, current.AppPassword, current.Database);
                    currentRootOk = true;
                    log.Info("已采用旧版配置中的 root 密码进行重置");
                }

                if (!currentRootOk)
                {
                    if (string.IsNullOrWhiteSpace(current.RootNewPassword))
                    {
                        current.RootNewPassword = MySqlServiceHelper.GenerateRandomPassword();
                        log.Info($"已生成新的 root 密码: {current.RootNewPassword}");
                    }

                    if (!MySqlManager.ForceResetRootPassword(log.Info))
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "强制重置 root 密码失败，未能完成业务账号修复。", "数据库配置检查", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                if (!MySqlManager.CreateOrUpdateUser(log.Info))
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "业务账号重置失败，请检查 root 密码和 MySQL 服务状态。", "数据库配置检查", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                SyncManagedServiceConfigs();
                if (legacy != null || HasLegacyConfig)
                {
                    SyncLegacyMySqlProfileSafely(CreateLegacyProfileFromCurrent(), true);
                    MessageBox.Show(Application.Current.GetActiveWindow(),"数据库业务账号已重置并同步到两边配置。", "数据库配置检查", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "数据库业务账号已重置，当前服务配置已更新，未检测到旧版配置文件。", "数据库配置检查", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                RefreshAll();
            }
            finally
            {
                SetBusy(false);
            }
        }

        public int GetConfiguredMySqlPort()
        {
            return MySqlManager.GetConfiguredPort(Config.MySqlPort);
        }

        private void DoDeleteUser()
        {
            MySqlManager.DeleteUser(log.Info);
        }

        private void GenerateRandomRootPassword()
        {
            MySqlManager.GenerateRandomRootPassword(log.Info);
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

        private LegacyMySqlProfile CreateLegacyProfileFromCurrent()
        {
            var current = MySqlManager.Config;
            return new LegacyMySqlProfile
            {
                Host = current.Host,
                Port = current.Port,
                AppUser = current.AppUser,
                AppPassword = current.AppPassword,
                RootPassword = current.RootPassword,
                Database = current.Database
            };
        }
    }
}
