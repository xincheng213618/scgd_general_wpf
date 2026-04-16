using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.UI;
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

            string targetDir = Path.Combine(Directory.GetParent(basePath)?.FullName ?? basePath, "Mysql");

            var credentials = CreateFreshMySqlInstallCredentials();

            SetBusy(true, "正在安装 MySQL...");
            try
            {
                MySqlHelper.Port = GetConfiguredMySqlPort();
                bool result = await MySqlHelper.InstallFromZipAsync(
                    dlg.FileName,
                    targetDir,
                    AddLog,
                    credentials.RootPassword,
                    credentials.AppUser,
                    credentials.AppPassword,
                    credentials.Database);

                if (result)
                {
                    AddLog("MySQL 安装成功");
                    ApplyInstalledMySqlCredentials(
                        credentials.RootPassword,
                        credentials.AppUser,
                        credentials.AppPassword,
                        credentials.Database,
                        MySqlHelper.BasePath);
                    AddLog($"MySQL root 密码: {credentials.RootPassword}");
                    AddLog($"MySQL 业务账号: {credentials.AppUser}");
                    AddLog($"MySQL 业务密码: {credentials.AppPassword}");
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
            var mySqlConfig = MySqlSetting.Instance.MySqlConfig;
            string timestamp = DateTime.Now.ToString("yyyyMMdd'T'HHmmss");
            string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "Backup");
            string bakFile = Path.Combine(backupDir, $"color_vision_{timestamp}.sql");

            MySqlHelper.BackupDatabase(mySqlConfig.UserName, mySqlConfig.UserPwd, mySqlConfig.Database, bakFile, AddLog);
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

            var mySqlConfig = MySqlSetting.Instance.MySqlConfig;
            MySqlHelper.RestoreDatabase(mySqlConfig.UserName, mySqlConfig.UserPwd, mySqlConfig.Database, filePath, AddLog);
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

            var mySqlConfig = MySqlSetting.Instance.MySqlConfig;
            MySqlHelper.ExecuteSqlFile(mySqlConfig.UserPwd, mySqlConfig.Database, filePath, AddLog);
        }

        private void DoSetRootPassword()
        {
            if (string.IsNullOrWhiteSpace(MySqlRootNewPassword))
            {
                AddLog("请先输入新 root 密码");
                return;
            }

            bool ok = MySqlHelper.TrySetRootPassword(MySqlRootPassword, MySqlRootNewPassword, AddLog);
            if (ok)
            {
                MySqlRootPassword = MySqlRootNewPassword;
                PersistRootConfig(MySqlRootPassword, MySqlSetting.Instance.MySqlConfig.Host, GetConfiguredMySqlPort(), MySqlSetting.Instance.MySqlConfig.Database);
                SaveMySqlSetting();
                SyncLegacyAppConfig();
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

            if (string.IsNullOrWhiteSpace(MySqlRootNewPassword))
            {
                AddLog("请先输入新 root 密码");
                return;
            }

            bool ok = MySqlHelper.ForceResetRootPassword(MySqlRootNewPassword, AddLog);
            if (ok)
            {
                MySqlRootPassword = MySqlRootNewPassword;
                PersistRootConfig(MySqlRootPassword, MySqlSetting.Instance.MySqlConfig.Host, GetConfiguredMySqlPort(), MySqlSetting.Instance.MySqlConfig.Database);
                SaveMySqlSetting();
                SyncLegacyAppConfig();
                RefreshMySqlStatus();
            }
        }

        private void DoCreateOrUpdateUser()
        {
            if (string.IsNullOrWhiteSpace(MySqlAppUser) || string.IsNullOrWhiteSpace(MySqlAppPassword) || string.IsNullOrWhiteSpace(MySqlDatabaseName))
            {
                AddLog("请填写用户、密码、数据库");
                return;
            }

            bool ok = MySqlHelper.CreateAppUser(MySqlRootPassword, MySqlAppUser, MySqlAppPassword, MySqlDatabaseName, AddLog);
            if (!ok)
            {
                AddLog("创建/更新业务用户失败，请确认 root 密码是否正确");
                return;
            }

            var cfg = MySqlSetting.Instance.MySqlConfig;
            cfg.Port = GetConfiguredMySqlPort();
            cfg.UserName = MySqlAppUser;
            cfg.UserPwd = MySqlAppPassword;
            cfg.Database = MySqlDatabaseName;
            SaveMySqlSetting();
            SyncLegacyAppConfig();
            SyncAllConfigs(false);
            AddLog("业务用户配置已更新到当前系统配置");
        }

        public (string RootPassword, string AppUser, string AppPassword, string Database) CreateFreshMySqlInstallCredentials()
        {
            var dbCfg = MySqlSetting.Instance.MySqlConfig;
            string database = !string.IsNullOrWhiteSpace(MySqlDatabaseName)
                ? MySqlDatabaseName.Trim()
                : (!string.IsNullOrWhiteSpace(dbCfg.Database) ? dbCfg.Database.Trim() : "color_vision");
            string appUser = !string.IsNullOrWhiteSpace(MySqlAppUser) && !string.Equals(MySqlAppUser, "root", StringComparison.OrdinalIgnoreCase)
                ? MySqlAppUser.Trim()
                : "cv";

            return (
                MySqlServiceHelper.GenerateRandomPassword(),
                appUser,
                MySqlServiceHelper.GenerateRandomPassword(),
                database);
        }

        public void ApplyInstalledMySqlCredentials(string rootPassword, string appUser, string appPassword, string database, string? installedBasePath = null)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                MySqlRootPassword = rootPassword;
                MySqlRootNewPassword = string.Empty;
                MySqlAppUser = appUser;
                MySqlAppPassword = appPassword;
                MySqlDatabaseName = database;
            });

            MySqlHelper.Port = GetConfiguredMySqlPort();
            if (!MySqlHelper.DetectFromRegistry() && !string.IsNullOrWhiteSpace(installedBasePath))
            {
                MySqlHelper.BasePath = installedBasePath;
            }

            PersistMySqlConfiguration(rootPassword, appUser, appPassword, database);
            SyncLegacyAppConfig();
            RefreshMySqlStatus();
            AddLog("MySQL 账号信息已持久化并回填到界面");
        }

        public int GetConfiguredMySqlPort()
        {
            int port = Config.MySqlPort;
            if (port <= 0)
            {
                port = MySqlSetting.Instance.MySqlConfig.Port;
            }
            return port > 0 ? port : 3306;
        }

        private void PersistMySqlConfiguration(string rootPassword, string appUser, string appPassword, string database)
        {
            var cfg = MySqlSetting.Instance.MySqlConfig;
            cfg.Host = string.IsNullOrWhiteSpace(cfg.Host) ? "127.0.0.1" : cfg.Host;
            cfg.Port = GetConfiguredMySqlPort();
            cfg.UserName = appUser;
            cfg.UserPwd = appPassword;
            cfg.Database = database;

            PersistRootConfig(rootPassword, cfg.Host, cfg.Port, database);
            SaveMySqlSetting();
        }

        private void SaveMySqlSetting()
        {
            ConfigHandler.GetInstance().Save<MySqlSetting>();
        }

        private static void PersistRootConfig(string rootPassword, string? host, int port, string? database)
        {
            var rootCfg = MySqlSetting.Instance.MySqlConfigs.FirstOrDefault(a => a.Name == "RootPath");
            if (rootCfg == null)
            {
                rootCfg = new MySqlConfig
                {
                    Name = "RootPath",
                    UserName = "root"
                };
                MySqlSetting.Instance.MySqlConfigs.Add(rootCfg);
            }

            rootCfg.Host = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host;
            rootCfg.Port = port > 0 ? port : 3306;
            rootCfg.UserName = "root";
            rootCfg.Database = string.IsNullOrWhiteSpace(database) ? MySqlSetting.Instance.MySqlConfig.Database : database;
            rootCfg.UserPwd = rootPassword;
        }

        private void DoDeleteUser()
        {
            if (string.IsNullOrWhiteSpace(MySqlAppUser))
            {
                AddLog("请先填写要删除的用户名");
                return;
            }
            bool ok = MySqlHelper.DeleteAppUser(MySqlRootPassword, MySqlAppUser, AddLog);
            if (ok)
                AddLog($"用户 {MySqlAppUser} 已删除");
        }

        private void GenerateRandomRootPassword()
        {
            MySqlRootNewPassword = MySqlServiceHelper.GenerateRandomPassword();
            AddLog($"已生成随机 root 密码: {MySqlRootNewPassword}");
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
                var dir = Directory.GetParent(dlg.FileName)?.Parent?.FullName;
                if (!string.IsNullOrEmpty(dir))
                {
                    MySqlHelper.BasePath = dir;
                    MySqlExePath = dlg.FileName;
                    RefreshMySqlStatus();
                }
            }
        }
    }
}
