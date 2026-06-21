using ColorVision.Database;
using ColorVision.UI;
using ColorVision.UI.ServiceHost;
using Newtonsoft.Json.Linq;
using System.IO;

namespace WindowsServicePlugin.ServiceManager
{
    public class MySqlServiceManager
    {
        private static readonly string[] ResetPreservedTables =
        [
            "t_scgd_sys_resource_group",
            "t_scgd_sys_resource",
            "t_scgd_sys_resource_tpa_dll",
            "t_scgd_sys_third_party_algorithms",
            "t_scgd_camera_license"
        ];

        public MySqlServiceConfig Config { get; } = MySqlServiceConfig.Instance;

        public MySqlServiceHelper Helper { get; } = new MySqlServiceHelper();

        public MySqlServiceManager()
        {
            MigrateFromLegacySettings();
        }

        public void Initialize(int fallbackPort)
        {
            MigrateFromLegacySettings();
            EnsureDefaultSqlScriptPath();
            Helper.Port = GetConfiguredPort(fallbackPort);

            if (!Helper.DetectFromRegistry() && !string.IsNullOrWhiteSpace(Config.InstallBasePath))
            {
                Helper.BasePath = Config.InstallBasePath;
            }
        }

        public async Task<bool> InstallFromZipViaServiceHostAsync(string zipFilePath, string baseLocation, Action<string> logCallback)
        {
            string targetDir = Directory.GetParent(baseLocation)?.FullName ?? baseLocation;
            var credentials = CreateFreshInstallCredentials();
            int port = GetConfiguredPort(ServiceManagerConfig.Instance.MySqlPort);
            Helper.Port = port;

            try
            {
                logCallback("正在通过 ColorVisionServiceHost 后台 ZIP 全安装 MySQL...");
                ServiceHostResponse response = await ColorVisionServiceHostClient.Default
                    .InstallMySqlFromZipAsync(
                        Helper.ServiceName,
                        zipFilePath,
                        targetDir,
                        port,
                        credentials.RootPassword,
                        credentials.AppUser,
                        credentials.AppPassword,
                        credentials.Database)
                    .ConfigureAwait(true);

                if (!response.Success)
                {
                    LogServiceHostFailure(response, logCallback);
                    return false;
                }

                string installedBasePath = response.Data?["installBasePath"]?.ToString() ?? Helper.BasePath;
                ApplyInstalledCredentials(
                    credentials.RootPassword,
                    credentials.AppUser,
                    credentials.AppPassword,
                    credentials.Database,
                    installedBasePath);

                RefreshConfigFromHelper();
                logCallback($"后台服务执行成功: {response.Message}");
                logCallback($"MySQL root 密码: {credentials.RootPassword}");
                logCallback($"MySQL 业务账号: {credentials.AppUser}");
                logCallback($"MySQL 业务密码: {credentials.AppPassword}");
                logCallback("MySQL 账号信息已保存到 MySqlServiceConfig");
                return true;
            }
            catch (Exception ex)
            {
                logCallback($"ColorVisionServiceHost 不可用或执行失败: {ex.Message}");
                logCallback("请先在“更新 -> ColorVision Service Host”中安装/更新后台服务。");
                return false;
            }
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
            Config.AppUser = appUser;
            Config.AppPassword = appPassword;
            Config.Database = database;
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
            EnsureDefaultSqlScriptPath();

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

        public async Task<bool> RegisterExistingServiceViaServiceHostAsync(Action<string> logCallback)
        {
            return await RepairMySqlViaServiceHostAsync("正在通过 ColorVisionServiceHost 后台注册 MySQL 服务...", logCallback).ConfigureAwait(true);
        }

        private async Task<bool> RepairMySqlViaServiceHostAsync(string startMessage, Action<string> logCallback)
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
                logCallback(startMessage);
                ServiceHostResponse response = await ColorVisionServiceHostClient.Default.RepairMySqlServiceAsync(Helper.ServiceName, mysqldExePath).ConfigureAwait(true);

                if (!response.Success)
                {
                    LogServiceHostFailure(response, logCallback);
                    return false;
                }

                logCallback($"后台服务执行成功: {response.Message}");
                RefreshConfigFromHelper();
                return true;
            }
            catch (Exception ex)
            {
                logCallback($"ColorVisionServiceHost 不可用或执行失败: {ex.Message}");
                logCallback("请先在“更新 -> ColorVision Service Host”中安装/更新后台服务。");
                return false;
            }
        }

        public async Task<bool> StartViaServiceHostAsync(Action<string> logCallback)
        {
            bool ok = await ServiceHostWindowsServiceController.ExecuteAsync(Config.ServiceName, ServiceHostServiceOperation.Start, logCallback, "MySQL").ConfigureAwait(true);
            if (ok)
                RefreshConfigFromHelper();

            return ok;
        }

        public async Task<bool> StopViaServiceHostAsync(Action<string> logCallback)
        {
            bool ok = await ServiceHostWindowsServiceController.ExecuteAsync(Config.ServiceName, ServiceHostServiceOperation.Stop, logCallback, "MySQL").ConfigureAwait(true);
            if (ok)
                RefreshConfigFromHelper();

            return ok;
        }

        public async Task<bool> UninstallViaServiceHostAsync(Action<string> logCallback)
        {
            bool ok = await ServiceHostWindowsServiceController.UninstallAsync(Config.ServiceName, logCallback, "MySQL").ConfigureAwait(true);
            if (ok)
            {
                Config.IsInstalled = false;
                Config.IsRunning = false;
                Config.Status = "未安装";
                Config.ExePath = string.Empty;
                Config.Version = string.Empty;
                SaveConfig();
            }

            return ok;
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
            if (IsColorVisionAllSql(filePath))
            {
                logCallback("检测到 color_vision_all.sql，切换为安全重置流程");
                return ResetDatabaseFromSqlFile(filePath, logCallback);
            }

            return ExecuteRootSqlFile(filePath, logCallback, Config.Database);
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

            return ResetDatabaseFromSqlFile(sqlFilePath, logCallback);
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
            Config.AppUser = appUser;
            Config.AppPassword = appPassword;
            Config.Database = database;
            SaveConfig();
        }

        public bool ApplyRootPassword(Action<string> logCallback)
        {
            if (string.IsNullOrWhiteSpace(Config.RootPassword))
            {
                logCallback("请先输入 root 密码");
                return false;
            }

            if (IsRootPasswordUsable(Config.RootPassword))
            {
                SaveConfig();
                logCallback("root 密码验证通过并已保存");
                return true;
            }

            logCallback("root 密码不可用，准备通过后台服务强制重置为当前输入的 root 密码...");
            return ForceResetRootPassword(Config.RootPassword, logCallback);
        }

        private bool ForceResetRootPassword(string targetPassword, Action<string> logCallback)
        {
            return ForceResetRootPasswordWithoutUacAsync(targetPassword, logCallback).GetAwaiter().GetResult();
        }

        private async Task<bool> ForceResetRootPasswordWithoutUacAsync(string targetPassword, Action<string> logCallback)
        {
            if (string.IsNullOrWhiteSpace(targetPassword))
            {
                logCallback("请先输入 root 密码");
                return false;
            }

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

            bool serviceExists = Config.IsInstalled || Helper.IsInstalled;
            bool shouldRestart = Config.IsRunning || Helper.IsRunning;
            try
            {
                if (serviceExists)
                {
                    bool stopped = await ServiceHostWindowsServiceController.ExecuteAsync(Helper.ServiceName, ServiceHostServiceOperation.Stop, logCallback, "MySQL").ConfigureAwait(false);
                    if (!stopped && Helper.IsRunning)
                    {
                        logCallback("后台停止 MySQL 服务失败，无法继续重置 root 密码");
                        return false;
                    }
                }

                bool ok = Helper.ResetRootPasswordWithStoppedService(targetPassword, logCallback);
                if (!ok)
                {
                    return false;
                }

                Config.RootPassword = targetPassword;
                SaveConfig();
                RefreshConfigFromHelper();
                logCallback("root 密码强制重置成功");
                return true;
            }
            finally
            {
                if (serviceExists && shouldRestart)
                {
                    await ServiceHostWindowsServiceController.ExecuteAsync(Helper.ServiceName, ServiceHostServiceOperation.Start, logCallback, "MySQL").ConfigureAwait(false);
                }
            }
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

        public void GenerateRandomRootPassword(Action<string> logCallback)
        {
            Config.RootPassword = MySqlServiceHelper.GenerateRandomPassword();
            SaveConfig();
            logCallback($"已生成随机 root 密码: {Config.RootPassword}");
        }

        public void SetSqlScriptPath(string sqlScriptPath)
        {
            Config.SqlScriptPath = sqlScriptPath;
            SaveConfig();
        }

        public void EnsureDefaultSqlScriptPath()
        {
            if (!string.IsNullOrWhiteSpace(Config.SqlScriptPath))
                return;

            string? sqlFilePath = ResolveResetDatabaseSqlPath();
            if (string.IsNullOrWhiteSpace(sqlFilePath))
                return;

            Config.SqlScriptPath = sqlFilePath;
            SaveConfig();
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

            return ResetDatabaseFromSqlFile(sqlFilePath, logCallback);
        }

        public bool InitializeColorVisionDatabase(string basePath, Action<string> logCallback)
        {
            string? sqlFilePath = ResolveColorVisionAllSqlPath(basePath);
            if (string.IsNullOrWhiteSpace(sqlFilePath))
            {
                logCallback("未找到 color_vision_all.sql，跳过数据库初始化脚本执行");
                return true;
            }

            if (!EnsureRootPasswordReady(logCallback))
            {
                return false;
            }

            logCallback("新安装 MySQL，直接执行数据库初始化脚本");
            return ExecuteRootSqlFile(sqlFilePath, logCallback, null, false);
        }

        private bool ExecuteRootSqlFile(string sqlFilePath, Action<string> logCallback)
        {
            return ExecuteRootSqlFile(sqlFilePath, logCallback, null, true);
        }

        private bool ExecuteRootSqlFile(string sqlFilePath, Action<string> logCallback, string? database)
        {
            return ExecuteRootSqlFile(sqlFilePath, logCallback, database, true);
        }

        private bool ExecuteRootSqlFile(string sqlFilePath, Action<string> logCallback, string? database, bool ensureRootPassword)
        {
            if (ensureRootPassword && !EnsureRootPasswordReady(logCallback))
            {
                return false;
            }

            logCallback($"使用 root 执行 SQL: {sqlFilePath}");
            return Helper.ExecuteSqlFile("root", Config.RootPassword, database, sqlFilePath, logCallback);
        }

        private bool ResetDatabaseFromSqlFile(string sqlFilePath, Action<string> logCallback)
        {
            if (!EnsureRootPasswordReady(logCallback))
            {
                return false;
            }

            if (!TryBackupResetPreservedData(logCallback, out string? preservedDataSql))
            {
                logCallback("重置前资源数据备份失败，已停止执行以避免数据丢失");
                return false;
            }

            if (!ExecuteRootSqlFile(sqlFilePath, logCallback, null, false))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(preservedDataSql))
            {
                logCallback("没有检测到需要回写的旧资源数据");
                return true;
            }

            logCallback($"正在回写资源数据: {preservedDataSql}");
            bool restored = Helper.ExecuteSqlFile("root", Config.RootPassword, Config.Database, preservedDataSql, logCallback);
            logCallback(restored ? "资源数据回写完成" : "资源数据回写失败");
            return restored;
        }

        private bool EnsureRootPasswordReady(Action<string> logCallback)
        {
            Helper.Port = GetConfiguredPort(ServiceManagerConfig.Instance.MySqlPort);
            if (!ResolveSavedMySqlBasePath(logCallback))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(Config.RootPassword) && IsRootPasswordUsable(Config.RootPassword))
            {
                logCallback("root 密码验证通过");
                return true;
            }

            logCallback("保存的 root 密码不可用，准备强制重置 root 密码...");
            string targetPassword = Config.RootPassword;
            if (string.IsNullOrWhiteSpace(targetPassword))
            {
                targetPassword = MySqlServiceHelper.GenerateRandomPassword();
                logCallback($"未保存 root 密码，已生成新的 root 密码: {targetPassword}");
            }

            if (!ForceResetRootPassword(targetPassword, logCallback))
            {
                logCallback("root 密码重置失败，无法继续执行 SQL");
                return false;
            }

            bool verified = IsRootPasswordUsable(Config.RootPassword);
            logCallback(verified ? "root 密码重置并验证通过" : "root 密码重置后仍无法连接");
            return verified;
        }

        private bool IsRootPasswordUsable(string rootPassword)
        {
            string configuredHost = string.IsNullOrWhiteSpace(Config.Host) ? "127.0.0.1" : Config.Host.Trim();
            return Helper.TestConnection(configuredHost, Helper.Port, "root", rootPassword, null)
                || (!string.Equals(configuredHost, "localhost", StringComparison.OrdinalIgnoreCase)
                    && Helper.TestConnection("localhost", Helper.Port, "root", rootPassword, null));
        }

        private bool TryBackupResetPreservedData(Action<string> logCallback, out string? backupFile)
        {
            backupFile = null;

            Helper.Port = GetConfiguredPort(ServiceManagerConfig.Instance.MySqlPort);
            if (!File.Exists(Helper.MysqlExePath))
            {
                Helper.DetectFromRegistry();
            }

            if (string.IsNullOrWhiteSpace(Config.RootPassword))
            {
                logCallback("未找到 root 密码，无法备份重置前资源数据");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Config.Database))
            {
                logCallback("未配置业务数据库名，跳过资源数据备份");
                return true;
            }

            if (!Helper.TryGetExistingTables("root", Config.RootPassword, Config.Database, ResetPreservedTables, out IReadOnlyList<string> existingTables, logCallback))
            {
                return false;
            }

            if (existingTables.Count == 0)
            {
                logCallback("未检测到旧资源数据表，跳过资源数据备份");
                return true;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd'T'HHmmss");
            string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "Backup");
            backupFile = Path.Combine(backupDir, $"color_vision_resources_{timestamp}.sql");

            if (!Helper.BackupDataTables("root", Config.RootPassword, Config.Database, backupFile, existingTables, logCallback))
            {
                backupFile = null;
                return false;
            }

            return true;
        }

        public static string? ResolveColorVisionAllSqlPath(string? basePath)
        {
            return EnumerateServiceInstallRoots(basePath)
                .Select(root => Path.Combine(root, "SQL", "color_vision_all.sql"))
                .FirstOrDefault(File.Exists);
        }

        private static bool IsColorVisionAllSql(string filePath)
        {
            return string.Equals(Path.GetFileName(filePath), "color_vision_all.sql", StringComparison.OrdinalIgnoreCase);
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

        private void RefreshConfigFromHelper()
        {
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
                : WinServiceHelper.GetServiceInstallPath(Config.ServiceName) ?? string.Empty;
            Config.ExePath = exePath;
            Config.Version = Config.IsInstalled && !string.IsNullOrWhiteSpace(exePath)
                ? WinServiceHelper.GetFileVersion(exePath)?.ToString() ?? string.Empty
                : string.Empty;

            RememberInstallBasePath(exePath);
            SaveConfig();
        }

        private static void LogServiceHostFailure(ServiceHostResponse response, Action<string> logCallback)
        {
            logCallback($"后台服务执行失败: {response.Message}");
            LogServiceHostProcessResults(response.Data?["processResults"], logCallback);
            if (response.Message.Contains("Unsupported command", StringComparison.OrdinalIgnoreCase))
            {
                logCallback("当前已安装的 ColorVisionServiceHost 版本过旧，请先在“更新 -> ColorVision Service Host”中重新安装/更新后台服务。");
            }
        }

        private static void LogServiceHostProcessResults(JToken? token, Action<string> logCallback)
        {
            if (token is not JArray results || results.Count == 0)
                return;

            int start = Math.Max(0, results.Count - 3);
            for (int i = start; i < results.Count; i++)
            {
                JToken item = results[i];
                string fileName = Path.GetFileName(item["fileName"]?.ToString() ?? string.Empty);
                string arguments = item["arguments"]?.ToString() ?? string.Empty;
                string exitCode = item["exitCode"]?.ToString() ?? string.Empty;
                string output = item["output"]?.ToString().Trim() ?? string.Empty;
                string error = item["error"]?.ToString().Trim() ?? string.Empty;
                logCallback($"  {fileName} {arguments} => {exitCode}");
                if (!string.IsNullOrWhiteSpace(output))
                    logCallback($"  stdout: {output}");
                if (!string.IsNullOrWhiteSpace(error))
                    logCallback($"  stderr: {error}");
            }
        }

        private static void SaveConfig()
        {
            ConfigHandler.GetInstance().Save<MySqlServiceConfig>();
        }
    }
}
