using ColorVision.Database;
using ColorVision.UI;
using System.IO;

namespace WindowsServicePlugin.ServiceManager
{
    public class MySqlServiceManager
    {
        public MySqlServiceConfig Config { get; } = MySqlServiceConfig.Instance;

        public MySqlServiceHelper Helper { get; } = new MySqlServiceHelper();

        public MySqlServiceManager()
        {
            MigrateFromLegacySettings();
        }

        public void Initialize(int fallbackPort)
        {
            MigrateFromLegacySettings();
            Helper.Port = GetConfiguredPort(fallbackPort);

            if (!Helper.DetectFromRegistry() && !string.IsNullOrWhiteSpace(Config.InstallBasePath))
            {
                Helper.BasePath = Config.InstallBasePath;
            }
        }

        public async Task<bool> InstallFromZipAsync(string zipFilePath, string baseLocation, Action<string> logCallback)
        {
            string targetDir = Directory.GetParent(baseLocation)?.FullName ?? baseLocation;
            var credentials = CreateFreshInstallCredentials();

            Helper.Port = GetConfiguredPort(ServiceManagerConfig.Instance.MySqlPort);
            bool result = await Helper.InstallFromZipAsync(
                zipFilePath,
                targetDir,
                logCallback,
                credentials.RootPassword,
                credentials.AppUser,
                credentials.AppPassword,
                credentials.Database);

            if (!result)
            {
                return false;
            }

            ApplyInstalledCredentials(
                credentials.RootPassword,
                credentials.AppUser,
                credentials.AppPassword,
                credentials.Database,
                Helper.BasePath);

            logCallback($"MySQL root 密码: {credentials.RootPassword}");
            logCallback($"MySQL 业务账号: {credentials.AppUser}");
            logCallback($"MySQL 业务密码: {credentials.AppPassword}");
            logCallback("MySQL 账号信息已保存到 MySqlServiceConfig");
            return true;
        }

        public (string RootPassword, string AppUser, string AppPassword, string Database) CreateFreshInstallCredentials()
        {
            string database = string.IsNullOrWhiteSpace(Config.Database) ? "color_vision_4xx" : Config.Database.Trim();
            string appUser = string.IsNullOrWhiteSpace(Config.AppUser) || string.Equals(Config.AppUser, "root", StringComparison.OrdinalIgnoreCase)
                ? "cv"
                : Config.AppUser.Trim();

            return (
                MySqlServiceHelper.GenerateRandomPassword(),
                appUser,
                MySqlServiceHelper.GenerateRandomPassword(),
                database);
        }

        public void ApplyInstalledCredentials(string rootPassword, string appUser, string appPassword, string database, string? installedBasePath = null)
        {
            Config.RootPassword = rootPassword;
            Config.RootNewPassword = string.Empty;
            Config.AppUser = appUser;
            Config.AppPassword = appPassword;
            Config.Database = database;
            Config.Host = Config.Host;
            Config.Port = GetConfiguredPort(ServiceManagerConfig.Instance.MySqlPort);

            if (!string.IsNullOrWhiteSpace(installedBasePath))
            {
                Config.InstallBasePath = installedBasePath;
            }

            Helper.Port = Config.Port;
            if (!Helper.DetectFromRegistry() && !string.IsNullOrWhiteSpace(installedBasePath))
            {
                Helper.BasePath = installedBasePath;
            }

            SaveConfig();
        }

        public int GetConfiguredPort(int fallbackPort = 3306)
        {
            int port = Config.Port > 0 ? Config.Port : fallbackPort;
            return port > 0 ? port : 3306;
        }

        public void RefreshStatus(IEnumerable<ServiceEntry> services, int fallbackPort)
        {
            Helper.Port = GetConfiguredPort(fallbackPort);

            if (!Helper.DetectFromRegistry() && !string.IsNullOrWhiteSpace(Config.InstallBasePath))
            {
                Helper.BasePath = Config.InstallBasePath;
            }

            Config.ServiceName = Helper.ServiceName;
            Config.IsInstalled = Helper.IsInstalled;
            Config.IsRunning = Helper.IsRunning;
            Config.Status = Config.IsRunning ? "运行中" : (Config.IsInstalled ? "已停止" : "未安装");

            string exePath = File.Exists(Helper.MysqldExePath)
                ? Helper.MysqldExePath
                : services.FirstOrDefault(s => string.Equals(s.ServiceName, Config.ServiceName, StringComparison.OrdinalIgnoreCase))?.ExePath ?? string.Empty;
            Config.ExePath = exePath;
            Config.Version = Config.IsInstalled && !string.IsNullOrWhiteSpace(exePath)
                ? WinServiceHelper.GetFileVersion(exePath)?.ToString() ?? string.Empty
                : string.Empty;

            RememberInstallBasePath(exePath);
        }

        public bool Start(Action<string> logCallback)
        {
            return Helper.Start(logCallback);
        }

        public bool RegisterExistingService(Action<string> logCallback)
        {
            Helper.Port = GetConfiguredPort(ServiceManagerConfig.Instance.MySqlPort);
            if (!ResolveExistingMySqlBasePath(logCallback))
            {
                return false;
            }

            bool ok = Helper.RegisterExistingService(logCallback);
            if (ok)
            {
                Config.ServiceName = Helper.ServiceName;
                Config.InstallBasePath = Helper.BasePath;
                Config.ExePath = Helper.MysqldExePath;
                Config.IsInstalled = Helper.IsInstalled;
                Config.IsRunning = Helper.IsRunning;
                Config.Status = Config.IsRunning ? "运行中" : (Config.IsInstalled ? "已停止" : "未安装");
                SaveConfig();
            }
            return ok;
        }

        public async Task<bool> RepairOrRestartViaServiceHostAsync(Action<string> logCallback)
        {
            Helper.Port = GetConfiguredPort(ServiceManagerConfig.Instance.MySqlPort);
            if (!ResolveSavedMySqlBasePath(logCallback))
            {
                return false;
            }

            string mysqldExePath = Helper.MysqldExePath;
            if (!File.Exists(mysqldExePath))
            {
                logCallback($"mysqld.exe 不存在: {mysqldExePath}");
                return false;
            }

            try
            {
                logCallback("正在通过 ColorVisionServiceHost 后台修复/重启 MySQL 服务...");
                ColorVisionServiceHostResponse response = await ColorVisionServiceHostPipeClient.SendAsync(
                    "repair-mysql-service",
                    new
                    {
                        serviceName = Helper.ServiceName,
                        mysqldExePath,
                        timeoutSeconds = 60,
                    },
                    TimeSpan.FromSeconds(90));

                if (!response.Success)
                {
                    logCallback($"后台服务执行失败: {response.Message}");
                    return false;
                }

                logCallback($"后台服务执行成功: {response.Message}");
                Config.ServiceName = Helper.ServiceName;
                Config.InstallBasePath = Helper.BasePath;
                Config.ExePath = mysqldExePath;
                Config.IsInstalled = Helper.IsInstalled;
                Config.IsRunning = Helper.IsRunning;
                Config.Status = Config.IsRunning ? "运行中" : (Config.IsInstalled ? "已停止" : "未安装");
                SaveConfig();
                return true;
            }
            catch (Exception ex)
            {
                logCallback($"ColorVisionServiceHost 不可用或执行失败: {ex.Message}");
                return false;
            }
        }

        public bool Stop(Action<string> logCallback)
        {
            return Helper.Stop(logCallback);
        }

        public bool Uninstall(Action<string> logCallback)
        {
            bool result = Helper.Uninstall(logCallback);
            if (result)
            {
                Config.IsInstalled = false;
                Config.IsRunning = false;
                Config.Status = "未安装";
                Config.ExePath = string.Empty;
                Config.Version = string.Empty;
            }
            return result;
        }

        public bool BackupDatabase(Action<string> logCallback)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd'T'HHmmss");
            string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "Backup");
            string bakFile = Path.Combine(backupDir, $"color_vision_{timestamp}.sql");

            return Helper.BackupDatabase(Config.AppUser, Config.AppPassword, Config.Database, bakFile, logCallback);
        }

        public bool RestoreDatabase(string filePath, Action<string> logCallback)
        {
            return Helper.RestoreDatabase(Config.AppUser, Config.AppPassword, Config.Database, filePath, logCallback);
        }

        public bool ExecuteSqlFile(string filePath, Action<string> logCallback)
        {
            return Helper.ExecuteSqlFile(Config.AppUser, Config.AppPassword, Config.Database, filePath, logCallback);
        }

        public static string? ResolveResetDatabaseSqlPath()
        {
            return ResolveColorVisionAllSqlPath(ServiceManagerConfig.Instance.BaseLocation);
        }

        public bool ResetDatabaseFromServiceSql(Action<string> logCallback)
        {
            string? sqlFilePath = ResolveResetDatabaseSqlPath();
            if (string.IsNullOrWhiteSpace(sqlFilePath))
            {
                logCallback("未找到 color_vision_all.sql，无法重置数据库");
                return false;
            }

            return ExecuteRootSqlFile(sqlFilePath, logCallback);
        }

        public bool TestConnection(string host, int port, string userName, string password, string? database, Action<string>? logCallback = null)
        {
            Helper.Port = GetConfiguredPort(port);
            if (!File.Exists(Helper.MysqlExePath))
            {
                Helper.DetectFromRegistry();
            }

            return Helper.TestConnection(host, Helper.Port, userName, password, database, logCallback);
        }

        public void UpdateStoredCredentials(string host, int port, string rootPassword, string appUser, string appPassword, string database)
        {
            Config.Host = host;
            Config.Port = GetConfiguredPort(port);
            Config.RootPassword = rootPassword;
            Config.RootNewPassword = string.Empty;
            Config.AppUser = appUser;
            Config.AppPassword = appPassword;
            Config.Database = database;
            SaveConfig();
        }

        public bool SetRootPassword(Action<string> logCallback)
        {
            if (string.IsNullOrWhiteSpace(Config.RootNewPassword))
            {
                logCallback("请先输入新 root 密码");
                return false;
            }

            bool ok = Helper.TrySetRootPassword(Config.RootPassword, Config.RootNewPassword, logCallback);
            if (!ok)
            {
                return false;
            }

            Config.RootPassword = Config.RootNewPassword;
            Config.RootNewPassword = string.Empty;
            SaveConfig();
            return true;
        }

        public bool ForceResetRootPassword(Action<string> logCallback)
        {
            if (string.IsNullOrWhiteSpace(Config.RootNewPassword))
            {
                logCallback("请先输入新 root 密码");
                return false;
            }

            bool ok = Helper.ForceResetRootPassword(Config.RootNewPassword, logCallback);
            if (!ok)
            {
                return false;
            }

            Config.RootPassword = Config.RootNewPassword;
            Config.RootNewPassword = string.Empty;
            SaveConfig();
            return true;
        }

        public bool CreateOrUpdateUser(Action<string> logCallback)
        {
            if (string.IsNullOrWhiteSpace(Config.AppUser) || string.IsNullOrWhiteSpace(Config.AppPassword) || string.IsNullOrWhiteSpace(Config.Database))
            {
                logCallback("请填写用户、密码、数据库");
                return false;
            }

            bool ok = Helper.CreateAppUser(Config.RootPassword, Config.AppUser, Config.AppPassword, Config.Database, logCallback);
            if (!ok)
            {
                logCallback("创建/更新业务用户失败，请确认 root 密码是否正确");
                return false;
            }

            SaveConfig();
            return true;
        }

        public bool DeleteUser(Action<string> logCallback)
        {
            if (string.IsNullOrWhiteSpace(Config.AppUser))
            {
                logCallback("请先填写要删除的用户名");
                return false;
            }

            return Helper.DeleteAppUser(Config.RootPassword, Config.AppUser, logCallback);
        }

        public void GenerateRandomRootPassword(Action<string> logCallback)
        {
            Config.RootNewPassword = MySqlServiceHelper.GenerateRandomPassword();
            logCallback($"已生成随机 root 密码: {Config.RootNewPassword}");
        }

        public void SetManualBasePath(string mysqldExePath)
        {
            string? dir = Directory.GetParent(mysqldExePath)?.Parent?.FullName;
            if (string.IsNullOrWhiteSpace(dir))
            {
                return;
            }

            Config.InstallBasePath = dir;
            Helper.BasePath = dir;
            SaveConfig();
        }

        private bool ResolveExistingMySqlBasePath(Action<string> logCallback)
        {
            if (!string.IsNullOrWhiteSpace(Config.ExePath) && File.Exists(Config.ExePath))
            {
                SetManualBasePath(Config.ExePath);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(Config.InstallBasePath)
                && File.Exists(Path.Combine(Config.InstallBasePath, "bin", "mysqld.exe")))
            {
                Helper.BasePath = Config.InstallBasePath;
                return true;
            }

            if (Helper.DetectFromRegistry())
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(ServiceManagerConfig.Instance.BaseLocation)
                && Helper.DetectFromServicePath(ServiceManagerConfig.Instance.BaseLocation))
            {
                return true;
            }

            if (Directory.Exists(@"D:\CVService") && Helper.DetectFromServicePath(@"D:\CVService"))
            {
                return true;
            }

            logCallback("未找到已有 MySQL 文件，请先浏览选择 mysqld.exe 或检查安装目录");
            return false;
        }

        private bool ResolveSavedMySqlBasePath(Action<string> logCallback)
        {
            if (!string.IsNullOrWhiteSpace(Config.InstallBasePath)
                && File.Exists(Path.Combine(Config.InstallBasePath, "bin", "mysqld.exe")))
            {
                Helper.BasePath = Config.InstallBasePath;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(Config.ExePath) && File.Exists(Config.ExePath))
            {
                RememberInstallBasePath(Config.ExePath);
                Helper.BasePath = Directory.GetParent(Config.ExePath)?.Parent?.FullName ?? Helper.BasePath;
                return true;
            }

            if (Helper.DetectFromRegistry())
            {
                RememberInstallBasePath(Helper.MysqldExePath);
                return true;
            }

            logCallback("未找到已保存的 MySQL 路径，请在 MySQL 页先浏览选择 mysqld.exe。");
            return false;
        }

        private void RememberInstallBasePath(string? mysqldExePath)
        {
            if (string.IsNullOrWhiteSpace(mysqldExePath) || !File.Exists(mysqldExePath))
            {
                return;
            }

            string? basePath = Directory.GetParent(mysqldExePath)?.Parent?.FullName;
            if (string.IsNullOrWhiteSpace(basePath))
            {
                return;
            }

            Helper.BasePath = basePath;
            if (!string.Equals(Config.InstallBasePath, basePath, StringComparison.OrdinalIgnoreCase))
            {
                Config.InstallBasePath = basePath;
                SaveConfig();
            }
        }

        public bool ExecuteColorVisionAllSql(string basePath, Action<string> logCallback)
        {
            string? sqlFilePath = ResolveColorVisionAllSqlPath(basePath);
            if (string.IsNullOrWhiteSpace(sqlFilePath))
            {
                logCallback("未找到 color_vision_all.sql，跳过数据库初始化脚本执行");
                return true;
            }

            return ExecuteRootSqlFile(sqlFilePath, logCallback);
        }

        private bool ExecuteRootSqlFile(string sqlFilePath, Action<string> logCallback)
        {
            Helper.Port = GetConfiguredPort(ServiceManagerConfig.Instance.MySqlPort);
            if (!File.Exists(Helper.MysqlExePath))
            {
                Helper.DetectFromRegistry();
            }

            if (string.IsNullOrWhiteSpace(Config.RootPassword))
            {
                logCallback("未找到 root 密码，无法执行数据库重置脚本");
                return false;
            }

            logCallback($"执行 SQL: {sqlFilePath}");
            return Helper.ExecuteSqlFile("root", Config.RootPassword, null, sqlFilePath, logCallback);
        }

        public static string? ResolveColorVisionAllSqlPath(string? basePath)
        {
            return EnumerateServiceInstallRoots(basePath)
                .Select(root => Path.Combine(root, "SQL", "color_vision_all.sql"))
                .FirstOrDefault(File.Exists);
        }

        private void MigrateFromLegacySettings()
        {
            bool changed = false;
            var legacyConfig = MySqlSetting.Instance.MySqlConfig;

            if (string.IsNullOrWhiteSpace(Config.Host) && !string.IsNullOrWhiteSpace(legacyConfig.Host))
            {
                Config.Host = legacyConfig.Host;
                changed = true;
            }
            if (Config.Port <= 0 && legacyConfig.Port > 0)
            {
                Config.Port = legacyConfig.Port;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(Config.AppUser) && !string.IsNullOrWhiteSpace(legacyConfig.UserName))
            {
                Config.AppUser = legacyConfig.UserName;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(Config.AppPassword) && !string.IsNullOrWhiteSpace(legacyConfig.UserPwd))
            {
                Config.AppPassword = legacyConfig.UserPwd;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(Config.Database) && !string.IsNullOrWhiteSpace(legacyConfig.Database))
            {
                Config.Database = legacyConfig.Database;
                changed = true;
            }

            var legacyRoot = MySqlSetting.Instance.MySqlConfigs.FirstOrDefault(item =>
                string.Equals(item.Name, MySqlServiceConfig.RootProfileName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.UserName, "root", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(Config.RootPassword) && legacyRoot != null && !string.IsNullOrWhiteSpace(legacyRoot.UserPwd))
            {
                Config.RootPassword = legacyRoot.UserPwd;
                changed = true;
            }

            if (changed)
            {
                Config.Host = Config.Host;
                Config.Port = GetConfiguredPort(ServiceManagerConfig.Instance.MySqlPort);
                SaveConfig();
            }
        }

        private static List<string> EnumerateServiceInstallRoots(string? basePath)
        {
            var roots = new List<string>();

            void AddRoot(string? root)
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    return;
                }

                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(root);
                }
                catch
                {
                    return;
                }

                if (Directory.Exists(fullPath) && !roots.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
                {
                    roots.Add(fullPath);
                }
            }

            if (!string.IsNullOrWhiteSpace(basePath))
            {
                AddRoot(ResolveServiceInstallRoot(basePath));
                AddRoot(basePath);
                AddRoot(Path.Combine(basePath, "CVWindowsService"));
            }

            foreach (string serviceName in new[] { "RegistrationCenterService", "CVMainService_x64", "CVMainService_dev" })
            {
                string? exePath = WinServiceHelper.GetServiceInstallPath(serviceName);
                string? serviceRoot = string.IsNullOrWhiteSpace(exePath)
                    ? null
                    : Directory.GetParent(exePath)?.Parent?.FullName;
                AddRoot(serviceRoot);
            }

            AddRoot(@"D:\CVService");

            return roots;
        }

        private static string ResolveServiceInstallRoot(string basePath)
        {
            string nested = Path.Combine(basePath, "CVWindowsService");
            string[] candidates =
            [
                basePath,
                nested
            ];

            string[] markers =
            [
                "RegWindowsService",
                "CVMainWindowsService_x64",
                "CVMainWindowsService_dev"
            ];

            foreach (string candidate in candidates)
            {
                if (!Directory.Exists(candidate))
                {
                    continue;
                }

                if (markers.Any(marker => Directory.Exists(Path.Combine(candidate, marker))))
                {
                    return candidate;
                }
            }

            return basePath;
        }

        private static void SaveConfig()
        {
            ConfigHandler.GetInstance().Save<MySqlServiceConfig>();
        }
    }
}
