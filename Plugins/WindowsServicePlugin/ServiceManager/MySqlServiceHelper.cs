using ColorVision.Common.Utilities;
using ColorVision.Database;
using log4net;
using MySqlConnector;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// MySQL 本地命令辅助：账号、SQL、备份、还原和 root 密码修复。
    /// </summary>
    public class MySqlServiceHelper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MySqlServiceHelper));

        public string ServiceName { get; set; } = "MySQL";
        public string BasePath { get; set; } = string.Empty;
        public int Port { get; set; } = 3306;

        public string MysqldExePath => Path.Combine(BasePath, "bin", "mysqld.exe");
        public string MysqlExePath => Path.Combine(BasePath, "bin", "mysql.exe");
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

        public bool IsInstalled => WinServiceHelper.IsServiceExisted(ServiceName);
        public bool IsRunning => WinServiceHelper.IsServiceRunning(ServiceName);

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
        /// 执行 SQL 脚本文件
        /// </summary>
        public bool ExecuteSqlFile(string userName, string password, string? database, string sqlFilePath, Action<string> logCallback)
        {
            if (!File.Exists(sqlFilePath))
            {
                logCallback($"SQL 文件不存在: {sqlFilePath}");
                return false;
            }
            try
            {
                logCallback($"正在执行 SQL 脚本: {Path.GetFileName(sqlFilePath)}...");
                string mysqlPath = File.Exists(MysqlExePath)
                    ? MysqlExePath
                    : MySqlLocalConfig.Instance.MysqlPath;

                if (string.IsNullOrWhiteSpace(mysqlPath) || !File.Exists(mysqlPath))
                {
                    logCallback("找不到 mysql 客户端");
                    return false;
                }

                var (sqlText, encodingName) = ReadSqlFileText(sqlFilePath);

                var psi = new ProcessStartInfo
                {
                    FileName = mysqlPath,
                    WorkingDirectory = Path.GetDirectoryName(mysqlPath) ?? BasePath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardInputEncoding = new UTF8Encoding(false),
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                psi.ArgumentList.Add("-P");
                psi.ArgumentList.Add(Port.ToString());
                psi.ArgumentList.Add("-u");
                psi.ArgumentList.Add(userName);
                if (!string.IsNullOrEmpty(password))
                {
                    psi.ArgumentList.Add($"-p{password}");
                }
                psi.ArgumentList.Add("--default-character-set=utf8mb4");
                if (!string.IsNullOrWhiteSpace(database))
                {
                    psi.ArgumentList.Add(database);
                }

                using var process = Process.Start(psi);
                if (process == null)
                {
                    logCallback("无法启动 mysql 客户端");
                    return false;
                }

                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                Exception? writeException = null;
                try
                {
                    process.StandardInput.Write(sqlText);
                }
                catch (IOException ex)
                {
                    writeException = ex;
                }
                finally
                {
                    process.StandardInput.Close();
                }

                bool finished = process.WaitForExit(600000);
                if (!finished)
                {
                    try
                    {
                        process.Kill(true);
                    }
                    catch
                    {
                    }

                    logCallback("SQL 脚本执行超时");
                    return false;
                }

                string stdout = stdoutTask.GetAwaiter().GetResult();
                string stderr = stderrTask.GetAwaiter().GetResult();

                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    logCallback(stdout.Trim());
                }
                if (process.ExitCode != 0)
                {
                    if (!string.IsNullOrWhiteSpace(stderr))
                    {
                        logCallback(stderr.Trim());
                    }
                    logCallback($"SQL 脚本执行失败，退出码: {process.ExitCode}");
                    return false;
                }

                if (writeException != null)
                {
                    logCallback($"SQL 输入提前结束: {writeException.Message}");
                    return false;
                }

                logCallback($"SQL 脚本执行完成（源编码: {encodingName}，输入编码: UTF-8）");
                return true;
            }
            catch (Exception ex)
            {
                logCallback($"执行 SQL 失败: {ex.Message}");
                return false;
            }
        }

        private static (string Text, string EncodingName) ReadSqlFileText(string sqlFilePath)
        {
            byte[] bytes = File.ReadAllBytes(sqlFilePath);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return (Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), "UTF-8 BOM");
            }

            var strictUtf8 = new UTF8Encoding(false, true);
            try
            {
                return (strictUtf8.GetString(bytes), "UTF-8");
            }
            catch (DecoderFallbackException)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                Encoding gb18030 = Encoding.GetEncoding("GB18030");
                return (gb18030.GetString(bytes), "GB18030");
            }
        }

        public bool TryGetExistingTables(string userName, string password, string database, IReadOnlyList<string> candidateTables, out IReadOnlyList<string> existingTables, Action<string> logCallback)
        {
            existingTables = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(database) || candidateTables.Count == 0)
            {
                return true;
            }

            try
            {
                var builder = new MySqlConnectionStringBuilder
                {
                    Server = "127.0.0.1",
                    Port = (uint)Port,
                    UserID = userName,
                    Password = password ?? string.Empty,
                    CharacterSet = "utf8mb4",
                    ConnectionTimeout = 5,
                    SslMode = MySqlSslMode.None,
                    Pooling = false
                };

                using var connection = new MySqlConnection(builder.ConnectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = BuildExistingTablesSql(candidateTables.Count);
                command.Parameters.AddWithValue("@schema", database);
                for (int i = 0; i < candidateTables.Count; i++)
                {
                    command.Parameters.AddWithValue($"@t{i}", candidateTables[i]);
                }

                HashSet<string> found = new(StringComparer.OrdinalIgnoreCase);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    found.Add(reader.GetString(0));
                }

                existingTables = candidateTables.Where(found.Contains).ToArray();
                return true;
            }
            catch (Exception ex)
            {
                logCallback($"检查可备份数据表失败: {ex.Message}");
                return false;
            }
        }

        private static string BuildExistingTablesSql(int tableCount)
        {
            string[] parameterNames = Enumerable.Range(0, tableCount).Select(i => $"@t{i}").ToArray();
            return $"SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = @schema AND TABLE_NAME IN ({string.Join(",", parameterNames)})";
        }

        public bool BackupDataTables(string userName, string password, string database, string outputFile, IReadOnlyList<string> tables, Action<string> logCallback)
        {
            if (tables.Count == 0)
            {
                logCallback("没有需要备份的数据表");
                return true;
            }

            try
            {
                string dumpPath = File.Exists(MysqldumpExePath)
                    ? MysqldumpExePath
                    : MySqlLocalConfig.Instance.MysqldumpPath;

                if (string.IsNullOrWhiteSpace(dumpPath) || !File.Exists(dumpPath))
                {
                    logCallback("找不到 mysqldump");
                    return false;
                }

                string? dir = Path.GetDirectoryName(outputFile);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var psi = new ProcessStartInfo
                {
                    FileName = dumpPath,
                    WorkingDirectory = Path.GetDirectoryName(dumpPath) ?? BasePath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                psi.ArgumentList.Add("-P");
                psi.ArgumentList.Add(Port.ToString());
                psi.ArgumentList.Add("-u");
                psi.ArgumentList.Add(userName);
                if (!string.IsNullOrEmpty(password))
                {
                    psi.ArgumentList.Add($"-p{password}");
                }

                psi.ArgumentList.Add("--default-character-set=utf8mb4");
                psi.ArgumentList.Add("--single-transaction");
                psi.ArgumentList.Add("--quick");
                psi.ArgumentList.Add("--skip-triggers");
                psi.ArgumentList.Add("--skip-lock-tables");
                psi.ArgumentList.Add("--skip-add-locks");
                psi.ArgumentList.Add("--no-create-info");
                psi.ArgumentList.Add("--complete-insert");
                psi.ArgumentList.Add("--replace");
                psi.ArgumentList.Add(database);
                foreach (string table in tables)
                {
                    psi.ArgumentList.Add(table);
                }

                logCallback($"正在备份资源数据表: {string.Join(", ", tables)}");
                using var process = Process.Start(psi);
                if (process == null)
                {
                    logCallback("无法启动 mysqldump");
                    return false;
                }

                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                bool finished = process.WaitForExit(600000);
                if (!finished)
                {
                    try
                    {
                        process.Kill(true);
                    }
                    catch
                    {
                    }

                    logCallback("资源数据备份超时");
                    return false;
                }

                string stdout = stdoutTask.GetAwaiter().GetResult();
                string stderr = stderrTask.GetAwaiter().GetResult();
                if (process.ExitCode != 0)
                {
                    if (!string.IsNullOrWhiteSpace(stderr))
                    {
                        logCallback(stderr.Trim());
                    }
                    logCallback($"资源数据备份失败，退出码: {process.ExitCode}");
                    return false;
                }

                StringBuilder sql = new();
                sql.AppendLine("SET NAMES utf8mb4;");
                sql.AppendLine("SET FOREIGN_KEY_CHECKS = 0;");
                sql.AppendLine(stdout);
                sql.AppendLine("SET FOREIGN_KEY_CHECKS = 1;");
                File.WriteAllText(outputFile, sql.ToString(), new UTF8Encoding(false));
                logCallback($"资源数据备份完成: {outputFile}");
                return true;
            }
            catch (Exception ex)
            {
                logCallback($"资源数据备份失败: {ex.Message}");
                return false;
            }
        }

        public bool TestConnection(string? host, int port, string userName, string password, string? database, Action<string>? logCallback = null)
        {
            string effectiveHost = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim();
            int effectivePort = port > 0 ? port : (Port > 0 ? Port : 3306);

            if (string.IsNullOrWhiteSpace(userName))
            {
                logCallback?.Invoke("用户名为空，无法校验数据库配置");
                return false;
            }

            try
            {
                var builder = new MySqlConnectionStringBuilder
                {
                    Server = effectiveHost,
                    Port = (uint)effectivePort,
                    UserID = userName,
                    Password = password ?? string.Empty,
                    Database = database ?? string.Empty,
                    CharacterSet = "utf8",
                    ConnectionTimeout = 5,
                    SslMode = MySqlSslMode.None,
                    Pooling = true,
                    AllowLoadLocalInfile = true
                };

                using var connection = new MySqlConnection(builder.ConnectionString);
                connection.Open();

                logCallback?.Invoke($"数据库配置可用: {userName}@{effectiveHost}:{effectivePort}{(string.IsNullOrWhiteSpace(database) ? string.Empty : $"/{database}")}");
                return true;
            }
            catch (MySqlException ex)
            {
                string detailMsg = ex.Number switch
                {
                    1045 => "账号或密码错误",
                    1049 => "指定的数据库不存在",
                    2003 => "无法连接到 MySQL 服务器，可能是端口未打开或网络不可达",
                    _ => $"MySqlException 错误码: {ex.Number}，错误信息: {ex.Message}"
                };
                log.Error($"数据库连接失败: {detailMsg}", ex);
                logCallback?.Invoke(detailMsg);
                return false;
            }
            catch (Exception ex)
            {
                log.Error("测试 MySQL 连接失败", ex);
                logCallback?.Invoke($"测试 MySQL 连接失败: {ex.Message}");
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

        public bool ResetRootPasswordWithStoppedService(string newPassword, Action<string> logCallback)
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
                if (Process.GetProcessesByName("mysqld").Length > 0)
                {
                    logCallback("检测到 mysqld 仍在运行，请先停止 MySQL 服务后再重置 root 密码");
                    return false;
                }

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
            }
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
                if (RunProcess(MysqlExePath, $"-P {Port} -u root -e \"SELECT 1;\"", BasePath, 5000))
                {
                    return true;
                }
                Thread.Sleep(1000);
            }
            return false;
        }

        private bool TryRunRootResetSql(string sql)
        {
            return RunProcess(MysqlExePath, $"-P {Port} -u root -e \"{sql}\"", BasePath, 10000);
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
