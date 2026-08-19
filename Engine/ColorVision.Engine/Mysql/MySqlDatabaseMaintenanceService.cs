using ColorVision.Engine.Services.RC;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ColorVision.Database
{
    /// <summary>
    /// ColorVision 数据库重置、SQL 恢复及服务 MySQL 配置同步的唯一实现。
    /// </summary>
    public static class MySqlDatabaseMaintenanceService
    {
        private static readonly string[] ManagedServiceNames =
        [
            "RegistrationCenterService",
            "CVMainService_x64",
            "CVMainService_dev"
        ];

        public static Task<string> RestoreSqlFileAsync(string sqlFilePath, MySqlConfig config, string mysqlPath, bool selectDatabase = true)
        {
            return MySqlLocalServicesManager.ExecuteSqlFileCoreAsync(sqlFilePath, config, mysqlPath, selectDatabase);
        }

        public static async Task<bool> ResetDatabaseFromSqlFileAsync(
            string sqlFilePath,
            string sourceDatabase,
            string targetDatabase,
            MySqlConfig rootConfig,
            string mysqlPath,
            string mysqldumpPath,
            string backupDirectory,
            Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(rootConfig);
            if (string.IsNullOrWhiteSpace(sourceDatabase) || string.IsNullOrWhiteSpace(targetDatabase))
            {
                logCallback?.Invoke("源数据库或目标数据库名称为空，无法执行数据库更新");
                return false;
            }

            string fullSqlPath = Path.GetFullPath(sqlFilePath);
            if (!File.Exists(fullSqlPath))
            {
                logCallback?.Invoke($"SQL 文件不存在: {fullSqlPath}");
                return false;
            }

            try
            {
                logCallback?.Invoke($"数据库更新路径: {sourceDatabase} -> {targetDatabase}");
                if (!string.Equals(sourceDatabase, targetDatabase, StringComparison.OrdinalIgnoreCase)
                    && !CanConnectToDatabase(rootConfig, sourceDatabase, logCallback))
                {
                    logCallback?.Invoke($"跨版本更新的源数据库 {sourceDatabase} 不存在或无法连接，已停止更新");
                    return false;
                }

                string? preservedDataSql = await BackupPreservedDataAsync(
                    sourceDatabase,
                    rootConfig,
                    mysqldumpPath,
                    backupDirectory,
                    logCallback).ConfigureAwait(false);

                logCallback?.Invoke($"使用 root 执行数据库重置脚本: {fullSqlPath}");
                await MySqlLocalServicesManager.ExecuteSqlFileCoreAsync(
                    fullSqlPath,
                    CloneConfig(rootConfig, sourceDatabase),
                    mysqlPath,
                    selectDatabase: false).ConfigureAwait(false);

                if (!CanConnectToDatabase(rootConfig, targetDatabase, logCallback))
                {
                    logCallback?.Invoke($"安装版本没有创建预期目标数据库 {targetDatabase}，已停止资源数据回写");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(preservedDataSql))
                {
                    logCallback?.Invoke("没有检测到需要回写的旧资源数据");
                    return true;
                }

                logCallback?.Invoke($"正在回写资源数据: {preservedDataSql}");
                await MySqlLocalServicesManager.ExecuteSqlFileCoreAsync(
                    preservedDataSql,
                    CloneConfig(rootConfig, targetDatabase),
                    mysqlPath,
                    selectDatabase: true).ConfigureAwait(false);
                logCallback?.Invoke("资源数据回写完成");
                return true;
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"数据库重置失败: {ex.Message}");
                return false;
            }
        }

        public static IReadOnlyList<string> SynchronizeInstalledServiceConfigs(MySqlConfig config, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(config);
            ServiceConfig serviceConfig = ServiceConfig.Instance;
            Dictionary<string, string?> executablePaths = new(StringComparer.OrdinalIgnoreCase)
            {
                [ManagedServiceNames[0]] = serviceConfig.RegistrationCenterService,
                [ManagedServiceNames[1]] = serviceConfig.CVMainService_x64,
                [ManagedServiceNames[2]] = serviceConfig.CVMainService_dev,
            };

            List<string> updatedFiles = [];
            foreach ((string serviceName, string? executablePath) in executablePaths)
            {
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    logCallback?.Invoke($"{serviceName}: 未配置安装路径，跳过 MySQL 配置同步");
                    continue;
                }

                string? serviceDirectory = Directory.Exists(executablePath)
                    ? Path.GetFullPath(executablePath)
                    : Path.GetDirectoryName(Path.GetFullPath(executablePath));
                if (string.IsNullOrWhiteSpace(serviceDirectory))
                {
                    logCallback?.Invoke($"{serviceName}: 无法解析安装目录，跳过 MySQL 配置同步");
                    continue;
                }

                string configPath = Path.Combine(serviceDirectory, "cfg", "MySql.config");
                if (!File.Exists(configPath))
                {
                    logCallback?.Invoke($"{serviceName}: 未找到 {configPath}，跳过 MySQL 配置同步");
                    continue;
                }

                UpdateConfigFile(configPath, config);
                updatedFiles.Add(configPath);
                logCallback?.Invoke($"{serviceName}: MySQL 配置同步完成 ({configPath})");
            }

            return updatedFiles;
        }

        public static void UpdateConfigFile(string configPath, MySqlConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            string fullConfigPath = Path.GetFullPath(configPath);
            if (!File.Exists(fullConfigPath))
                return;

            XDocument document = XDocument.Load(fullConfigPath, LoadOptions.PreserveWhitespace);
            IEnumerable<XElement>? settings = document.Element("configuration")?.Element("appSettings")?.Elements("add");
            if (settings == null)
                throw new InvalidDataException($"MySql.config 缺少 appSettings: {fullConfigPath}");

            foreach (XElement setting in settings)
            {
                string? key = setting.Attribute("key")?.Value;
                string? value = key switch
                {
                    "Host" => config.Host,
                    "Port" => config.Port.ToString(CultureInfo.InvariantCulture),
                    "User" => config.UserName,
                    "Password" => config.UserPwd,
                    "Database" => config.Database,
                    _ => null
                };
                if (value != null)
                    setting.SetAttributeValue("value", value);
            }

            document.Save(fullConfigPath, SaveOptions.DisableFormatting);
        }

        private static async Task<string?> BackupPreservedDataAsync(
            string sourceDatabase,
            MySqlConfig rootConfig,
            string mysqldumpPath,
            string backupDirectory,
            Action<string>? logCallback)
        {
            MySqlConfig sourceConfig = CloneConfig(rootConfig, sourceDatabase);
            IReadOnlyList<string> existingTables = GetExistingTables(sourceConfig, MySqlLocalServicesManager.MigrationBackupTableNames);
            if (existingTables.Count == 0)
            {
                logCallback?.Invoke("未检测到旧资源数据表，跳过资源数据备份");
                return null;
            }

            Directory.CreateDirectory(backupDirectory);
            string backupFile = Path.Combine(backupDirectory, $"color_vision_resources_{DateTime.Now:yyyyMMdd'T'HHmmssfff}_{Guid.NewGuid():N}.sql");
            string partFile = backupFile + ".part";
            logCallback?.Invoke($"正在备份重置前资源数据: {backupFile}");

            try
            {
                await MySqlLocalServicesManager.RunMysqlDumpAsync(
                    partFile,
                    existingTables,
                    replaceExistingRows: true,
                    sourceConfig,
                    mysqldumpPath,
                    dataOnly: true).ConfigureAwait(false);

                string dependencySql = MySqlLocalServicesManager.BuildMigrationDictionaryDependencyStatements(
                    MySqlControl.GetConnectionString(sourceConfig, 5),
                    logCallback);
                if (!string.IsNullOrWhiteSpace(dependencySql))
                {
                    string dependencyScript = MySqlProtocolDefaults.CreateScript(
                        "-- Referenced template dictionary dependencies",
                        dependencySql);
                    await File.AppendAllTextAsync(partFile, Environment.NewLine + dependencyScript, MySqlProtocolDefaults.ScriptEncoding).ConfigureAwait(false);
                }

                File.Move(partFile, backupFile);
                logCallback?.Invoke($"重置前资源数据备份完成，共 {existingTables.Count} 张表");
                return backupFile;
            }
            catch
            {
                try
                {
                    if (File.Exists(partFile))
                        File.Delete(partFile);
                }
                catch
                {
                }
                throw;
            }
        }

        private static IReadOnlyList<string> GetExistingTables(MySqlConfig config, IReadOnlyList<string> tableNames)
        {
            using SqlSugarClient database = new(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(config, 5),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true
            });
            DataTable result = database.Ado.GetDataTable(
                "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @database AND TABLE_TYPE = 'BASE TABLE'",
                new SugarParameter("@database", config.Database));
            HashSet<string> existing = result.Rows.Cast<DataRow>()
                .Select(row => Convert.ToString(row["TABLE_NAME"], CultureInfo.InvariantCulture))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return tableNames.Where(existing.Contains).ToArray();
        }

        private static bool CanConnectToDatabase(MySqlConfig rootConfig, string databaseName, Action<string>? logCallback)
        {
            try
            {
                MySqlConfig config = CloneConfig(rootConfig, databaseName);
                using SqlSugarClient database = new(new ConnectionConfig
                {
                    ConnectionString = MySqlControl.GetConnectionString(config, 5),
                    DbType = SqlSugar.DbType.MySql,
                    IsAutoCloseConnection = true
                });
                database.Ado.GetDataTable("SELECT 1");
                logCallback?.Invoke($"数据库 {databaseName} 连接验证通过");
                return true;
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"数据库 {databaseName} 连接验证失败: {ex.Message}");
                return false;
            }
        }

        private static MySqlConfig CloneConfig(MySqlConfig source, string database)
        {
            return new MySqlConfig
            {
                Name = source.Name,
                Host = source.Host,
                Port = source.Port,
                UserName = source.UserName,
                UserPwd = source.UserPwd,
                Database = database,
                DbType = source.DbType
            };
        }
    }
}
