using ColorVision.Common.MVVM;
using log4net;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;


namespace ColorVision.Engine.MySql
{

    public class MySqlControl: ViewModelBase, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MySqlControl));
        private static MySqlControl _instance;
        private static readonly object _locker = new();
        public static MySqlControl GetInstance() { lock (_locker) { return _instance ??= new MySqlControl(); } }

        public MySqlConnection MySqlConnection { get; set; }

        public static MySqlConfig Config => MySqlSetting.Instance.MySqlConfig;

        private Timer timer;
        public MySqlControl()
        {
            timer = new Timer(ReConnect, null, 0, MySqlSetting.Instance.ReConnectTime);
            MySqlSetting.Instance.ReConnectTimeChanged += (s, e) =>
            {
                timer.Change(0, MySqlSetting.Instance.ReConnectTime);
            };
        }
        public void ReConnect(object? o)
        {
            if (IsConnect)
            {
                ReConnect();
            }
        }

        public event EventHandler MySqlConnectChanged;

        public bool IsConnect { get => _IsConnect; private set { _IsConnect = value; NotifyPropertyChanged(); } }
        private bool _IsConnect;
        private static readonly char[] separator = new[] { ';' };

        private Task<bool> ReConnect()
        {
            {
                string connStr = GetConnectionString(Config);
                try
                {
                    IsConnect = false;
                    MySqlConnection = new MySqlConnection() { ConnectionString = connStr };
                    MySqlConnection.Open();
                    IsConnect = true;
                    log.Info($"数据库连接成功:{connStr}");
                    return Task.FromResult(true);
                }
                catch (Exception ex)
                {
                    IsConnect = false;
                    log.Error(ex);
                    return Task.FromResult(false);
                }
            }
        }


        public Task<bool> Connect()
        {
            string connStr = GetConnectionString(Config);
            try
            {
                IsConnect = false;
                MySqlConnection = new MySqlConnection() { ConnectionString = connStr  };
                MySqlConnection.Open();
                
                ///https://blog.csdn.net/a79412906/article/details/8971534
                ///https://bugs.mysql.com/bug.php?Id=2400
                //BatchExecuteQuery("SET SESSION  interactive_timeout=31536000;SET SESSION  wait_timeout=2147424;");

                //BatchExecuteQuery("SHOW VARIABLES LIKE 'interactive_timeout';SHOW VARIABLES LIKE 'wait_timeout';");

                Application.Current.Dispatcher.Invoke(() => MySqlConnectChanged?.Invoke(MySqlConnection, new EventArgs()));
                IsConnect = true;
                log.Info($"数据库连接成功:{connStr}");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                IsConnect = false;
                log.Error(ex);
                return Task.FromResult(false);
            }
        }

        public List<string> GetFilteredTableNames()
        {
            List<string> tableNames = new List<string>();

            string connectionString = $"Server={MySqlSetting.Instance.MySqlConfig.Host};Database={MySqlSetting.Instance.MySqlConfig.Database};User ID={MySqlSetting.Instance.MySqlConfig.UserName};Password={MySqlSetting.Instance.MySqlConfig.UserPwd};";

            string query = @"
            SELECT TABLE_NAME 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_SCHEMA = @databaseName AND TABLE_TYPE = 'BASE TABLE'";

            using (MySqlCommand command = new MySqlCommand(query, MySqlConnection))
            {
                command.Parameters.AddWithValue("@databaseName", MySqlSetting.Instance.MySqlConfig.Database);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string tableName = reader.GetString(0);
                        tableNames.Add(tableName);
                    }
                }
            }
            // 移除包含特定前缀的表
            tableNames = tableNames
                    .Where(name => !name.Contains("t_scgd_sys_config") && !name.Contains("t_scgd_rc"))
                    .ToList();

            return tableNames;
        }

        public List<string> GetFilteredResourceTableNames()
        {
            List<string> tableNames = new List<string>();

            string connectionString = $"Server={MySqlSetting.Instance.MySqlConfig.Host};Database={MySqlSetting.Instance.MySqlConfig.Database};User ID={MySqlSetting.Instance.MySqlConfig.UserName};Password={MySqlSetting.Instance.MySqlConfig.UserPwd};";

            string query = @"
            SELECT TABLE_NAME 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_SCHEMA = @databaseName AND TABLE_TYPE = 'BASE TABLE'";

            using (MySqlCommand command = new MySqlCommand(query, MySqlConnection))
            {
                command.Parameters.AddWithValue("@databaseName", MySqlSetting.Instance.MySqlConfig.Database);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string tableName = reader.GetString(0);
                        tableNames.Add(tableName);
                    }
                }
            }
            // 移除包含特定前缀的表
            tableNames = tableNames
                    .Where(name => !name.Contains("t_scgd_sys_config") && !name.Contains("t_scgd_rc"))
                    .ToList();

            return tableNames;
        }


        public static string GetConnectionString() => GetConnectionString(Config);

        public static string GetConnectionString(MySqlConfig MySqlConfig,int timeout = 3 )
        {
            string connStr = $"server={MySqlConfig.Host};port={MySqlConfig.Port};uid={MySqlConfig.UserName};pwd={MySqlConfig.UserPwd};database={MySqlConfig.Database};charset=utf8;Connect Timeout={timeout};SSL Mode =None;Pooling=true";
            return connStr;
        }

        public static bool TestConnect(MySqlConfig MySqlConfig)  
        {
            MySqlConnection MySqlConnection;
            string connStr = GetConnectionString(MySqlConfig,1);
            try
            {
                log.Info($"Test数据库连接信息:{connStr}");
                MySqlConnection = new MySqlConnection() { ConnectionString = connStr };
                MySqlConnection.Open();
                if (string.IsNullOrEmpty(MySqlConfig.Database))
                {
                    return false;
                }
                // Query to check if the database exists
                string query = $"SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{MySqlConfig.Database}'";
                using (var command = new MySqlCommand(query, MySqlConnection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            log.Info("Database exists.");
                            return true;
                        }
                        else
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(),"Database does not exist.");
                            log.Warn("Database does not exist.");
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return false;
            }
        }

        public void Close()
        {
            MySqlConnection.Close();
        }

        public int ExecuteNonQuery(string sql, Dictionary<string, object>? param = null)
        {
            int count = -1;
            try
            {
                MySqlCommand command = new(sql, MySqlConnection);
                if (param != null)
                {
                    foreach (var item in param)
                    {
                        command.Parameters.AddWithValue(item.Key, item.Value);
                    }
                }
                count = command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            return count;
        }
        public void BatchExecuteQuery(string sqlBatch)
        {
            // Split the entire SQL batch into individual SQL statements
            var statements = sqlBatch.Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var sql in statements)
            {
                try
                {
                    // Trim whitespace from the SQL statement
                    string trimmedSql = sql.Trim();
                    if (string.IsNullOrEmpty(trimmedSql))
                        continue;

                    using (MySqlCommand command = new(trimmedSql, MySqlConnection))
                    {
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            // Print column names
                            var columnNames = Enumerable.Range(0, reader.FieldCount)
                                                        .Select(reader.GetName)
                                                        .ToArray();
                            log.Info("Column Names: " + string.Join(", ", columnNames));

                            // Print each row
                            while (reader.Read())
                            {
                                var rowValues = Enumerable.Range(0, reader.FieldCount)
                                                          .Select(reader.GetValue)
                                                          .ToArray();
                                log.Info("Row Values: " + string.Join(", ", rowValues));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log the error information without affecting the execution of subsequent statements
                    log.Error($"SQL execution failed.\nError: {ex.Message}\nFailed SQL: {sql.Trim()}\n");
                }
            }
        }

        public int BatchExecuteNonQuery(string sqlBatch)
        {
            // 将整个SQL批次按照分号拆分为单个SQL语句
            var statements = sqlBatch.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            int totalCount = 0;
            foreach (var sql in statements)
            {
                try
                {
                    // 去除SQL语句两端的空白字符
                    string trimmedSql = sql.Trim();
                    if (string.IsNullOrEmpty(trimmedSql))
                        continue;

                    using (MySqlCommand command = new(trimmedSql, MySqlConnection))
                    {
                        int count = command.ExecuteNonQuery();
                        totalCount += count;
                        log.Info($"SQL执行成功。\n受影响的行数: {count}\n执行的SQL语句: {trimmedSql}\n");
                    }
                }
                catch (Exception ex)
                {
                    // 记录错误信息，但不影响后续语句的执行
                    log.Info( $"SQL执行失败。\n错误信息: {ex.Message}\n出错的SQL语句: {sql.Trim()}\n");
                    // 您也可以选择记录到日志或其他处理方式
                }
            }
            log.Info($"总共受影响的行数: {totalCount}\n");
            return totalCount;
        }


        public void EnsureLocalInfile()
        {
            string checkLocalInfile = "SHOW GLOBAL VARIABLES LIKE 'local_infile';";
            using var cmdCheck = new MySqlCommand(checkLocalInfile, MySqlConnection);
            using var reader = cmdCheck.ExecuteReader();
            if (reader.Read())
            {
                string localInfileValue = reader["Value"].ToString();
                log.Info($"Current local_infile Value: {localInfileValue}");

                // 如果local_infile的值为OFF或0，设置为1
                if (localInfileValue == "OFF" || localInfileValue == "0")
                {
                    reader.Close(); // 关闭reader，因为我们要执行另一个命令

                    string setLocalInfile = "SET GLOBAL local_infile = 1;";
                    using var cmdSet = new MySqlCommand(setLocalInfile, MySqlConnection);
                    cmdSet.ExecuteNonQuery();
                    log.Info("local_infile has been set to 1.");
                }
            }
        }

        public void Dispose()
        {
            MySqlConnection.Dispose();
            timer?.Dispose();
            GC.SuppressFinalize(this);
        }

    }
}
