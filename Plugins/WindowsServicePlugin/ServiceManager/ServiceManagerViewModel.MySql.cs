using ColorVision.Common.Utilities;
using System.IO;
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
                bool result = await MySqlManager.InstallFromZipAsync(dlg.FileName, basePath, AddLog);
                if (result)
                {
                    AddLog("MySQL 安装成功");
                    SyncAllConfigs(false);
                    RefreshAll();
                }
                else
                {
                    AddLog("MySQL 安装失败");
                }
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void DoMySqlBackup()
        {
            MySqlManager.BackupDatabase(AddLog);
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

            MySqlManager.RestoreDatabase(filePath, AddLog);
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

            MySqlManager.ExecuteSqlFile(filePath, AddLog);
        }

        private void DoSetRootPassword()
        {
            if (MySqlManager.SetRootPassword(AddLog))
            {
                SyncLegacyAppConfig();
                RefreshMySqlStatus();
            }
        }

        private void DoForceResetRootPassword()
        {
            if (!Tool.IsAdministrator())
            {
                AddLog("重置 root 密码需要管理员权限，请使用管理员身份启动程序后重试");
                MessageBox.Show("重置 root 密码需要管理员权限，请使用管理员身份启动程序后重试。", "需要管理员权限", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MySqlManager.ForceResetRootPassword(AddLog))
            {
                SyncLegacyAppConfig();
                RefreshMySqlStatus();
            }
        }

        private void DoCreateOrUpdateUser()
        {
            if (MySqlManager.CreateOrUpdateUser(AddLog))
            {
                SyncLegacyAppConfig();
                SyncAllConfigs(false);
                AddLog("业务用户配置已更新到 MySqlServiceConfig");
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

                AddLog($"当前配置业务账号校验: {(currentAppOk ? "成功" : "失败")}");
                AddLog($"旧版配置业务账号校验: {(legacyAppOk ? "成功" : "失败")}");

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
                    AddLog("已采用旧版配置中的 root 密码进行重置");
                }

                if (!currentRootOk)
                {
                    if (!Tool.IsAdministrator())
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "业务账号重置需要 root 密码；当前未匹配到可用 root 密码，且强制重置 root 需要管理员权限。", "数据库配置检查", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(current.RootNewPassword))
                    {
                        current.RootNewPassword = MySqlServiceHelper.GenerateRandomPassword();
                        AddLog($"已生成新的 root 密码: {current.RootNewPassword}");
                    }

                    if (!MySqlManager.ForceResetRootPassword(AddLog))
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "强制重置 root 密码失败，未能完成业务账号修复。", "数据库配置检查", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                if (!MySqlManager.CreateOrUpdateUser(AddLog))
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
            MySqlManager.DeleteUser(AddLog);
        }

        private void GenerateRandomRootPassword()
        {
            MySqlManager.GenerateRandomRootPassword(AddLog);
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
