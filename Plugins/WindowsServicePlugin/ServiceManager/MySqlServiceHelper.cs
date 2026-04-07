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
        /// ZIP全安装: 解压、初始化、安装服务、启动、创建用户
        /// </summary>
        public async Task<bool> InstallFromZipAsync(string zipFilePath, string targetPath, Action<string> logCallback)
        {
            return await Task.Run(() =>
            {
                try
                {
                    logCallback("正在解压 MySQL...");
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, targetPath, true);
                    
                    // 查找解压后的mysql目录
                    var mysqlDirs = Directory.GetDirectories(targetPath, "mysql-*");
                    if (mysqlDirs.Length == 0)
                    {
                        logCallback("解压完成但未找到 MySQL 目录");
                        return false;
                    }
                    BasePath = mysqlDirs[0];

                    return DoFullInstall(logCallback);
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
        /// 完整安装流程：初始化 → 安装服务 → 启动 → 设置密码 → 创建用户
        /// </summary>
        public bool DoFullInstall(Action<string> logCallback)
        {
            if (!File.Exists(MysqldExePath))
            {
                logCallback($"mysqld.exe 不存在: {MysqldExePath}");
                return false;
            }

            // 1. 初始化(insecure模式，即空密码)
            logCallback("正在初始化 MySQL (--initialize-insecure)...");
            RunProcess(MysqldExePath, "--initialize-insecure", BasePath);

            // 2. 安装Windows服务
            logCallback($"正在安装 MySQL 服务 ({ServiceName})...");
            RunProcess(MysqldExePath, $"--install {ServiceName}", BasePath);

            // 3. 启动服务
            logCallback("正在启动 MySQL 服务...");
            Tool.ExecuteCommandAsAdmin($"net start {ServiceName}");

            // 等待启动
            for (int i = 0; i < 30; i++)
            {
                Thread.Sleep(1000);
                if (WinServiceHelper.IsServiceRunning(ServiceName))
                {
                    logCallback("MySQL 服务已启动");
                    return true;
                }
            }

            logCallback("MySQL 服务启动超时");
            return false;
        }

        /// <summary>
        /// 设置 root 密码
        /// </summary>
        public bool SetRootPassword(string newPassword, Action<string> logCallback)
        {
            try
            {
                logCallback("正在设置 root 密码...");
                string args = $"-u root password \"{newPassword}\"";
                var result = RunProcess(MysqladminExePath, args, BasePath);
                logCallback(result ? "root 密码设置成功" : "root 密码设置失败");
                return result;
            }
            catch (Exception ex)
            {
                logCallback($"设置密码失败: {ex.Message}");
                return false;
            }
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
                string args = $"-u root -p\"{rootPwd}\" -e \"{sql}\"";
                var result = RunProcess(MysqlExePath, args, BasePath);
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

        public bool TrySetRootPassword(string oldPassword, string newPassword, Action<string> logCallback)
        {
            try
            {
                string safePwd = EscapeSqlLiteral(newPassword);
                string sql = $"ALTER USER 'root'@'localhost' IDENTIFIED BY '{safePwd}'; FLUSH PRIVILEGES;";
                string args = string.IsNullOrWhiteSpace(oldPassword)
                    ? $"-u root -e \"{sql}\""
                    : $"-u root -p\"{oldPassword}\" -e \"{sql}\"";
                bool ok = RunProcess(MysqlExePath, args, BasePath);
                logCallback(ok ? "root 密码更新成功" : "root 密码更新失败");
                return ok;
            }
            catch (Exception ex)
            {
                logCallback($"更新 root 密码失败: {ex.Message}");
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
