using ColorVision.Common.Utilities;
using ColorVision.Database;
using log4net;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// MySQL服务管理：安装、初始化、启动、停止、备份、还原
    /// </summary>
    public class MySqlServiceHelper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MySqlServiceHelper));

        public string ServiceName { get; set; } = "MySQL";
        public string BasePath { get; set; } = string.Empty;
        public int Port { get; set; } = 3306;

        /// <summary>最近一次全新安装时自动生成的 root 密码，由调用方持久化</summary>
        public string LastGeneratedRootPassword { get; private set; } = string.Empty;

        public string MysqldExePath => Path.Combine(BasePath, "bin", "mysqld.exe");
        public string MysqlExePath => Path.Combine(BasePath, "bin", "mysql.exe");
        public string MysqladminExePath => Path.Combine(BasePath, "bin", "mysqladmin.exe");
        public string MysqldumpExePath => Path.Combine(BasePath, "bin", "mysqldump.exe");
        public string MyIniPath => Path.Combine(BasePath, "my.ini");

        /// <summary>
        /// 从注册表检测MySQL安装路径
        /// </summary>
        public bool DetectFromRegistry()
        {
            string[] serviceNames = ["MySQL", "MySQL57", "MySQL80"];
            foreach (var name in serviceNames)
            {
                var exePath = WinServiceHelper.GetServiceInstallPath(name);
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    ServiceName = name;
                    BasePath = Directory.GetParent(exePath)?.Parent?.FullName ?? "";
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 从CVWindowsService的Mysql子目录检测
        /// </summary>
        public bool DetectFromServicePath(string cvWindowsServicePath)
        {
            // 在 CVWindowsService 目录的同级或上级查找 mysql 文件夹
            var parent = Directory.GetParent(cvWindowsServicePath)?.FullName ?? cvWindowsServicePath;
            string[] possiblePaths =
            [
                Path.Combine(parent, "mysql-5.7.37-winx64"),
                Path.Combine(parent, "Mysql", "mysql-5.7.37-winx64"),
                Path.Combine(cvWindowsServicePath, "mysql-5.7.37-winx64"),
            ];

            foreach (var p in possiblePaths)
            {
                if (Directory.Exists(p) && File.Exists(Path.Combine(p, "bin", "mysqld.exe")))
                {
                    BasePath = p;
                    return true;
                }
            }
            return false;
        }

        public bool IsInstalled => WinServiceHelper.IsServiceExisted(ServiceName);
        public bool IsRunning => WinServiceHelper.IsServiceRunning(ServiceName);

        /// <summary>
        /// ZIP全安装: 停止/删除旧服务 → 解压 → 初始化 → 安装服务 → 启动 → 创建业务用户 → 设置随机 root 密码
        /// 参考 CVWinSMS.CVMysqlServiceManager.DoMysqlInstallZip
        /// </summary>
        public async Task<bool> InstallFromZipAsync(
            string zipFilePath,
            string targetPath,
            Action<string> logCallback,
            string appUser = "",
            string appPassword = "",
            string database = "color_vision")
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 1. 若旧服务存在，先停止并移除
                    if (WinServiceHelper.IsServiceExisted(ServiceName))
                    {
                        logCallback($"停止并删除已有 MySQL 服务 ({ServiceName})...");
                        WinServiceHelper.StopService(ServiceName, 30);
                        for (int w = 0; w < 15; w++)
                        {
                            Thread.Sleep(1000);
                            if (!WinServiceHelper.IsServiceRunning(ServiceName)) break;
                        }
                        // 优先用现有 exe 卸载，否则用 sc delete
                        if (File.Exists(MysqldExePath))
                            RunProcessAdmin(MysqldExePath, $"--remove {ServiceName}", Path.GetDirectoryName(MysqldExePath)!);
                        else
                            WinServiceHelper.UninstallService(ServiceName);
                    }

                    // 2. 解压
                    logCallback("正在解压 MySQL...");
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, targetPath, true);

                    // 3. 查找解压后的 mysql 目录（如 mysql-5.7.37-winx64）
                    var mysqlDirs = Directory.GetDirectories(targetPath, "mysql-*");
                    if (mysqlDirs.Length == 0)
                    {
                        logCallback("解压完成但未找到 MySQL 目录 (mysql-*)");
                        return false;
                    }
                    BasePath = mysqlDirs[0];
                    logCallback($"MySQL 目录: {BasePath}");

                    return DoFullInstall(logCallback, appUser, appPassword, database);
                }
                catch (Exception ex)
                {
                    logCallback($"MySQL 安装失败: {ex.Message}");
                    log.Error("MySQL ZIP安装失败", ex);
                    return false;
                }
            });
        }

        /// <summary>
        /// 完整安装流程：初始化 → 安装服务 → 启动 → 创建业务用户 → 设置随机 root 密码
        /// 参考 CVWinSMS.CVMysqlServiceManager.DoMysqlInitInstall
        /// </summary>
        public bool DoFullInstall(
            Action<string> logCallback,
            string appUser = "",
            string appPassword = "",
            string database = "color_vision")
        {
            if (!File.Exists(MysqldExePath))
            {
                logCallback($"mysqld.exe 不存在: {MysqldExePath}");
                return false;
            }

            string binDir = Path.GetDirectoryName(MysqldExePath)!;

            // 1. 初始化 (--initialize-insecure → root初始密码为空)
            logCallback("正在初始化 MySQL (--initialize-insecure)...");
            RunProcessAdmin(MysqldExePath, "--initialize-insecure", binDir);

            // 2. 安装 Windows 服务 (需要管理员)
            logCallback($"正在安装 MySQL 服务 ({ServiceName})...");
            RunProcessAdmin(MysqldExePath, $"--install {ServiceName}", binDir);

            // 3. 启动服务
            logCallback("正在启动 MySQL 服务...");
            Tool.ExecuteCommandAsAdmin($"net start {ServiceName}");

            // 等待启动
            bool started = false;
            for (int i = 0; i < 30; i++)
            {
                Thread.Sleep(1000);
                if (WinServiceHelper.IsServiceRunning(ServiceName))
                {
                    logCallback("MySQL 服务已启动");
                    started = true;
                    break;
                }
            }
            if (!started)
            {
                logCallback("MySQL 服务启动超时");
                return false;
            }

            // 4. 创建业务用户 (此时 root 密码为空)
            if (!string.IsNullOrWhiteSpace(appUser) && !string.IsNullOrWhiteSpace(appPassword))
            {
                logCallback($"正在创建业务用户 {appUser}...");
                CreateAppUser("", appUser, appPassword, database, logCallback);
            }

            // 5. 为 root 生成随机密码并设置 (使用 mysqladmin，参考 CVWinSMS.doMysqlRootPwdset)
            string newRootPwd = GenerateRandomPassword();
            logCallback($"正在设置随机 root 密码...");
            bool pwdOk = SetRootPasswordViaAdmin("", newRootPwd);
            if (pwdOk)
            {
                LastGeneratedRootPassword = newRootPwd;
                logCallback($"root 密码已设置 (请保存): {newRootPwd}");
            }
            else
            {
                logCallback("root 密码设置失败，当前 root 密码为空，请手动设置");
            }

            return true;
        }

        /// <summary>
        /// 使用 mysqladmin 设置/更改 root 密码（参考 CVWinSMS.doMysqlRootPwdset）
        /// </summary>
        public bool SetRootPasswordViaAdmin(string oldPassword, string newPassword)
        {
            // mysqladmin -P {port} -u root [-p"old"] password "new"
            string oldPart = string.IsNullOrEmpty(oldPassword) ? "" : $" -p\"{EscapeSqlLiteral(oldPassword)}\"";
            string args = $"-P {Port} -u root{oldPart} password \"{EscapeSqlLiteral(newPassword)}\"";
            return RunProcess(MysqladminExePath, args, Path.GetDirectoryName(MysqladminExePath)!);
        }

        /// <summary>
        /// 创建应用用户并授权
        /// </summary>
        public bool CreateAppUser(string rootPwd, string userName, string userPwd, string database, Action<string> logCallback)
        {
            try
            {
                logCallback($"正在创建用户 {userName}...");
                string safeDb = EscapeSqlIdentifier(database);
                string safeUser = EscapeSqlLiteral(userName);
                string safePwd = EscapeSqlLiteral(userPwd);
                string sql = $"CREATE DATABASE IF NOT EXISTS `{safeDb}` CHARACTER SET utf8mb4; " +
                    $"CREATE USER IF NOT EXISTS '{safeUser}'@'localhost' IDENTIFIED BY '{safePwd}'; " +
                    $"ALTER USER '{safeUser}'@'localhost' IDENTIFIED BY '{safePwd}'; " +
                    $"GRANT ALL PRIVILEGES ON `{safeDb}`.* TO '{safeUser}'@'localhost'; " +
                    $"CREATE USER IF NOT EXISTS '{safeUser}'@'%' IDENTIFIED BY '{safePwd}'; " +
                    $"ALTER USER '{safeUser}'@'%' IDENTIFIED BY '{safePwd}'; " +
                    $"GRANT ALL PRIVILEGES ON `{safeDb}`.* TO '{safeUser}'@'%'; " +
                    $"FLUSH PRIVILEGES;";
                string userArgs = string.IsNullOrEmpty(rootPwd)
                    ? $"-P {Port} -u root -e \"{sql}\""
                    : $"-P {Port} -u root -p\"{EscapeSqlLiteral(rootPwd)}\" -e \"{sql}\"";
                var result = RunProcess(MysqlExePath, userArgs, Path.GetDirectoryName(MysqlExePath)!);
                logCallback(result ? $"用户 {userName} 创建成功" : $"用户 {userName} 创建失败");
                return result;
            }
            catch (Exception ex)
            {
                logCallback($"创建用户失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 删除业务用户（localhost 和 % 两个主机）
        /// 参考 CVWinSMS.CVMysqlServiceManager.DoMysqlDelUser
        /// </summary>
        public bool DeleteAppUser(string rootPwd, string userName, Action<string> logCallback)
        {
            try
            {
                logCallback($"正在删除用户 {userName}...");
                string safeUser = EscapeSqlLiteral(userName);
                string sql = $"DROP USER IF EXISTS '{safeUser}'@'localhost'; DROP USER IF EXISTS '{safeUser}'@'%'; FLUSH PRIVILEGES;";
                string args = string.IsNullOrEmpty(rootPwd)
                    ? $"-P {Port} -u root -e \"{sql}\""
                    : $"-P {Port} -u root -p\"{EscapeSqlLiteral(rootPwd)}\" -e \"{sql}\"";
                bool ok = RunProcess(MysqlExePath, args, Path.GetDirectoryName(MysqlExePath)!);
                logCallback(ok ? $"用户 {userName} 已删除" : $"用户 {userName} 删除失败");
                return ok;
            }
            catch (Exception ex)
            {
                logCallback($"删除用户失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 执行 SQL 脚本文件
        /// </summary>
        public bool ExecuteSqlFile(string rootPwd, string database, string sqlFilePath, Action<string> logCallback)
        {
            if (!File.Exists(sqlFilePath))
            {
                logCallback($"SQL 文件不存在: {sqlFilePath}");
                return false;
            }
            try
            {
                logCallback($"正在执行 SQL 脚本: {Path.GetFileName(sqlFilePath)}...");
                string command = $"\"{MysqlExePath}\" -u root -p{rootPwd} {database} < \"{sqlFilePath}\"";
                Tool.ExecuteCommandUI(command);
                logCallback("SQL 脚本执行完成");
                return true;
            }
            catch (Exception ex)
            {
                logCallback($"执行 SQL 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 备份数据库
        /// </summary>
        public bool BackupDatabase(string user, string password, string database, string outputFile, Action<string> logCallback)
        {
            try
            {
                logCallback($"正在备份数据库 {database}...");
                string dumpPath = File.Exists(MysqldumpExePath)
                    ? MysqldumpExePath
                    : MySqlLocalConfig.Instance.MysqldumpPath;

                if (string.IsNullOrEmpty(dumpPath) || !File.Exists(dumpPath))
                {
                    logCallback("找不到 mysqldump");
                    return false;
                }

                string dir = Path.GetDirectoryName(outputFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string command = $"\"{dumpPath}\" -u {user} -p{password} {database} > \"{outputFile}\"";
                Tool.ExecuteCommandUI(command);
                logCallback($"备份完成: {outputFile}");
                return true;
            }
            catch (Exception ex)
            {
                logCallback($"备份失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 还原数据库
        /// </summary>
        public bool RestoreDatabase(string user, string password, string database, string sqlFile, Action<string> logCallback)
        {
            if (!File.Exists(sqlFile))
            {
                logCallback($"SQL 备份文件不存在: {sqlFile}");
                return false;
            }
            try
            {
                logCallback($"正在还原数据库 {database}...");
                string mysqlPath = File.Exists(MysqlExePath)
                    ? MysqlExePath
                    : MySqlLocalConfig.Instance.MysqlPath;

                if (string.IsNullOrEmpty(mysqlPath) || !File.Exists(mysqlPath))
                {
                    logCallback("找不到 mysql 客户端");
                    return false;
                }

                string command = $"\"{mysqlPath}\" -u {user} -p{password} {database} < \"{sqlFile}\"";
                Tool.ExecuteCommandUI(command);
                logCallback("数据库还原完成");
                return true;
            }
            catch (Exception ex)
            {
                logCallback($"还原失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 启动 MySQL 服务
        /// </summary>
        public bool Start(Action<string> logCallback)
        {
            logCallback($"正在启动 MySQL 服务 ({ServiceName})...");
            bool result = Tool.ExecuteCommandAsAdmin($"net start {ServiceName}");
            logCallback(result ? "MySQL 服务已启动" : "MySQL 服务启动失败");
            return result;
        }

        /// <summary>
        /// 停止 MySQL 服务
        /// </summary>
        public bool Stop(Action<string> logCallback)
        {
            logCallback($"正在停止 MySQL 服务 ({ServiceName})...");
            bool result = Tool.ExecuteCommandAsAdmin($"net stop {ServiceName}");
            logCallback(result ? "MySQL 服务已停止" : "MySQL 服务停止失败");
            return result;
        }

        /// <summary>
        /// 卸载 MySQL 服务
        /// </summary>
        public bool Uninstall(Action<string> logCallback)
        {
            logCallback($"正在卸载 MySQL 服务 ({ServiceName})...");
            if (IsRunning)
                Stop(logCallback);
            
            RunProcess(MysqldExePath, $"--remove {ServiceName}", BasePath);
            logCallback("MySQL 服务已卸载");
            return true;
        }

        /// <summary>
        /// 通过 mysqladmin 更新 root 密码（MySQL 5.7 兼容）
        /// </summary>
        public bool TrySetRootPassword(string oldPassword, string newPassword, Action<string> logCallback)
        {
            try
            {
                logCallback("正在设置 root 密码...");
                bool ok = SetRootPasswordViaAdmin(oldPassword, newPassword);
                logCallback(ok ? "root 密码设置成功" : "root 密码设置失败");
                return ok;
            }
            catch (Exception ex)
            {
                logCallback($"设置 root 密码失败: {ex.Message}");
                return false;
            }
        }

        public bool ForceResetRootPassword(string newPassword, Action<string> logCallback)
        {
            if (!File.Exists(MysqldExePath) || !File.Exists(MysqlExePath))
            {
                logCallback("未找到 mysqld 或 mysql 客户端，无法重置 root 密码");
                return false;
            }

            Process? skipGrantProcess = null;
            try
            {
                logCallback("开始强制重置 root 密码...");
                EnsureMySqlStopped(logCallback);

                var startInfo = new ProcessStartInfo
                {
                    FileName = MysqldExePath,
                    Arguments = BuildManualStartupArguments("--skip-grant-tables --console"),
                    WorkingDirectory = BasePath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                skipGrantProcess = Process.Start(startInfo);
                if (skipGrantProcess == null)
                {
                    logCallback("无法启动 mysqld 临时重置进程");
                    return false;
                }

                if (!WaitForMySqlReady())
                {
                    string stderr = skipGrantProcess.StandardError.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(stderr))
                    {
                        log.Warn(stderr);
                        logCallback($"mysqld 启动失败: {stderr.Trim()}");
                    }
                    return false;
                }

                string safePwd = EscapeSqlLiteral(newPassword);
                bool ok = TryRunRootResetSql($"FLUSH PRIVILEGES; ALTER USER 'root'@'localhost' IDENTIFIED BY '{safePwd}'; FLUSH PRIVILEGES;");
                if (!ok)
                {
                    ok = TryRunRootResetSql($"SET PASSWORD FOR 'root'@'localhost' = PASSWORD('{safePwd}'); FLUSH PRIVILEGES;");
                }
                if (!ok)
                {
                    ok = TryRunRootResetSql($"UPDATE mysql.user SET authentication_string=PASSWORD('{safePwd}') WHERE User='root' AND Host='localhost'; FLUSH PRIVILEGES;");
                }
                if (!ok)
                {
                    ok = TryRunRootResetSql($"UPDATE mysql.user SET Password=PASSWORD('{safePwd}') WHERE User='root' AND Host='localhost'; FLUSH PRIVILEGES;");
                }

                if (skipGrantProcess != null && !skipGrantProcess.HasExited)
                {
                    skipGrantProcess.Kill();
                    skipGrantProcess.WaitForExit(3000);
                }

                Thread.Sleep(1500);
                Start(logCallback);

                logCallback(ok ? "root 密码强制重置成功" : "root 密码强制重置失败");
                return ok;
            }
            catch (Exception ex)
            {
                logCallback($"强制重置 root 密码失败: {ex.Message}");
                return false;
            }
            finally
            {
                try
                {
                    if (skipGrantProcess != null && !skipGrantProcess.HasExited)
                    {
                        skipGrantProcess.Kill();
                        skipGrantProcess.WaitForExit(3000);
                    }
                }
                catch
                {
                }

                if (IsInstalled && !IsRunning)
                {
                    Start(logCallback);
                }
            }
        }

        private void EnsureMySqlStopped(Action<string> logCallback)
        {
            if (IsInstalled)
            {
                logCallback($"正在停止 MySQL 服务 ({ServiceName})...");
                if (!WinServiceHelper.StopService(ServiceName, 30))
                {
                    Tool.ExecuteCommandAsAdmin($"net stop {ServiceName}");
                }
            }

            for (int i = 0; i < 10; i++)
            {
                if (Process.GetProcessesByName("mysqld").Length == 0)
                {
                    return;
                }
                Thread.Sleep(1000);
            }

            logCallback("检测到 mysqld 进程未退出，正在强制结束...");
            WinServiceHelper.KillProcessByName("mysqld");
            Thread.Sleep(1500);
        }

        private string BuildManualStartupArguments(string extraArguments)
        {
            string defaults = File.Exists(MyIniPath) ? $"--defaults-file=\"{MyIniPath}\" " : string.Empty;
            return defaults + extraArguments;
        }

        private bool WaitForMySqlReady()
        {
            for (int i = 0; i < 20; i++)
            {
                if (RunProcess(MysqlExePath, "-u root -e \"SELECT 1;\"", BasePath, 5000))
                {
                    return true;
                }
                Thread.Sleep(1000);
            }
            return false;
        }

        private bool TryRunRootResetSql(string sql)
        {
            return RunProcess(MysqlExePath, $"-u root -e \"{sql}\"", BasePath, 10000);
        }

        /// <summary>
        /// 生成随机密码（参考 CVWinSMS.MySqlTools.GenerateRandomPassword）
        /// </summary>
        public static string GenerateRandomPassword(int length = 12)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#&";
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var buf = new byte[length];
            rng.GetBytes(buf);
            return new string(buf.Select(b => chars[b % chars.Length]).ToArray());
        }

        /// <summary>
        /// 以管理员权限运行进程（若当前已是管理员则直接运行，否则请求 UAC）
        /// </summary>
        private static bool RunProcessAdmin(string fileName, string arguments, string workingDir)
        {
            try
            {
                bool isAdmin = Tool.IsAdministrator();
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDir,
                    UseShellExecute = !isAdmin,
                    CreateNoWindow = isAdmin,
                    Verb = isAdmin ? "" : "runas"
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(60000);
                return p?.ExitCode == 0;
            }
            catch (Exception ex)
            {
                log.Error($"RunProcessAdmin {Path.GetFileName(fileName)} {arguments} failed", ex);
                return false;
            }
        }

        private static string EscapeSqlLiteral(string value)
        {
            return value.Replace("\\", "\\\\").Replace("'", "''");
        }

        private static string EscapeSqlIdentifier(string value)
        {
            return value.Replace("`", "``");
        }

        private static bool RunProcess(string fileName, string arguments, string workingDir, int timeoutMilliseconds = 60000)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var process = Process.Start(psi);
                process?.WaitForExit(timeoutMilliseconds);
                return process?.ExitCode == 0;
            }
            catch (Exception ex)
            {
                log.Error($"运行 {fileName} {arguments} 失败", ex);
                return false;
            }
        }
    }
}
