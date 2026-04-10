using ColorVision.Common.Utilities;
using ColorVision.Database;
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

            // 读取当前业务用户配置
            var dbCfg = MySqlSetting.Instance.MySqlConfig;
            string appUser = string.IsNullOrWhiteSpace(MySqlAppUser) ? dbCfg.UserName : MySqlAppUser;
            string appPassword = string.IsNullOrWhiteSpace(MySqlAppPassword) ? dbCfg.UserPwd : MySqlAppPassword;
            string database = string.IsNullOrWhiteSpace(MySqlDatabaseName) ? dbCfg.Database : MySqlDatabaseName;

            SetBusy(true, "正在安装 MySQL...");
            bool result = await MySqlHelper.InstallFromZipAsync(dlg.FileName, targetDir, AddLog, appUser, appPassword, database);
            if (result)
            {
                AddLog("MySQL 安装成功");

                // 持久化自动生成的 root 密码
                if (!string.IsNullOrWhiteSpace(MySqlHelper.LastGeneratedRootPassword))
                {
                    MySqlRootPassword = MySqlHelper.LastGeneratedRootPassword;
                    PersistRootConfig(MySqlRootPassword);
                    AddLog($"请保存 root 密码: {MySqlRootPassword}");
                }

                // 更新业务用户配置
                dbCfg.UserName = appUser;
                dbCfg.UserPwd = appPassword;
                dbCfg.Database = database;
                MySqlAppUser = appUser;
                MySqlAppPassword = appPassword;
                MySqlDatabaseName = database;

                SyncLegacyAppConfig();
                RefreshMySqlStatus();
            }
            SetBusy(false);
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
                PersistRootConfig(MySqlRootPassword);
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
                PersistRootConfig(MySqlRootPassword);
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
            cfg.UserName = MySqlAppUser;
            cfg.UserPwd = MySqlAppPassword;
            cfg.Database = MySqlDatabaseName;
            SyncLegacyAppConfig();
            AddLog("业务用户配置已更新到当前系统配置");
        }

        private static void PersistRootConfig(string rootPassword)
        {
            var rootCfg = MySqlSetting.Instance.MySqlConfigs.FirstOrDefault(a => a.Name == "RootPath");
            if (rootCfg == null)
            {
                rootCfg = new MySqlConfig
                {
                    Name = "RootPath",
                    Host = MySqlSetting.Instance.MySqlConfig.Host,
                    UserName = "root",
                    Database = MySqlSetting.Instance.MySqlConfig.Database,
                    UserPwd = rootPassword
                };
                MySqlSetting.Instance.MySqlConfigs.Add(rootCfg);
            }
            else
            {
                rootCfg.UserPwd = rootPassword;
            }
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
