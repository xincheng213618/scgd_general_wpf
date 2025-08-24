using ColorVision.Common.MVVM;
using log4net;
using MySql.Data.MySqlClient;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;


namespace ColorVision.Database
{

    public class MySqlControl: ViewModelBase, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MySqlControl));
        private static MySqlControl _instance;
        private static readonly object _locker = new();
        public static MySqlControl GetInstance() { lock (_locker) { return _instance ??= new MySqlControl(); } }

        public static MySqlConfig Config => MySqlSetting.Instance.MySqlConfig;
        private volatile MySqlConnection _conn; // 用于切换
        public MySqlConnection MySqlConnection => _conn;

        public SqlSugarClient DB { get; set; }

        public MySqlControl()
        {
            Task.Run(async () => 
            {
                await Task.Delay(10000); // 等待配置加载完成
                MySqlLocalServicesManager.GetInstance();
            });

        }

        public event EventHandler MySqlConnectChanged;

        public bool IsConnect { get => _IsConnect; private set { _IsConnect = value; OnPropertyChanged(); } }
        private bool _IsConnect;
        private static readonly char[] separator = new[] { ';' };

        public string ConnectionString { get; private set; }

        ///https://blog.csdn.net/a79412906/article/details/8971534
        ///https://bugs.mysql.com/bug.php?Id=2400
        //BatchExecuteQuery("SET SESSION  interactive_timeout=31536000;SET SESSION  wait_timeout=2147424;");

        //BatchExecuteQuery("SHOW VARIABLES LIKE 'interactive_timeout';SHOW VARIABLES LIKE 'wait_timeout';");

        public Task<bool> Connect()
        {
            string connStr = GetConnectionString(Config);
            try
            {
                IsConnect = false;
                var newConn = new MySqlConnection() { ConnectionString = connStr };
                newConn.Open();
                var oldConn = Interlocked.Exchange(ref _conn, newConn); // 原子切换
                IsConnect = true;
                if (ConnectionString != connStr)
                {
                    Application.Current.Dispatcher.BeginInvoke(() => MySqlConnectChanged?.Invoke(newConn, new EventArgs()));
                }
                ConnectionString = connStr;
                log.Info($"数据库连接成功:{connStr}");

                DB = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = GetConnectionString(Config),
                    DbType = SqlSugar.DbType.MySql,
                    IsAutoCloseConnection = true
                });
                

                return Task.FromResult(true);
            }
            catch (MySqlException ex)
            {
                IsConnect = false;
                string detailMsg = ex.Number switch
                {
                    1045 => "账号或密码错误",
                    1049 => "指定的数据库不存在",
                    2003 => "无法连接到MySQL服务器，可能是端口未打开或网络不可达",
                    _ => $"MySqlException 错误码: {ex.Number}，错误信息: {ex.Message}"
                };
                log.Error($"数据库连接失败: {detailMsg}. 连接串: {connStr}");
                log.Error(ex);
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                IsConnect = false;
                log.Error($"数据库连接发生未知异常: {ex.Message}. 连接串: {connStr}");
                log.Error(ex);
                return Task.FromResult(false);
            }
        }



        public static string GetConnectionString() => GetConnectionString(Config);

        public static string GetConnectionString(MySqlConfig MySqlConfig,int timeout = 1)
        {
            string connStr = $"server={MySqlConfig.Host};port={MySqlConfig.Port};uid={MySqlConfig.UserName};pwd={MySqlConfig.UserPwd};database={MySqlConfig.Database};charset=utf8;Connect Timeout={timeout};SSL Mode =None;Pooling=true;AllowLoadLocalInfile=True";
            return connStr;
        }

        public static void TestConnect(MySqlConfig mySqlConfig)  
        {
            string connStr = GetConnectionString(mySqlConfig, 2);
            log.Info($"Test数据库连接信息:{connStr}");

            if (string.IsNullOrEmpty(mySqlConfig.Database))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "数据库名不能为空");
                });
                return;
            }

            try
            {
                var db = new SqlSugar.SqlSugarClient(new SqlSugar.ConnectionConfig
                {
                    ConnectionString = connStr,
                    DbType = SqlSugar.DbType.MySql,
                    IsAutoCloseConnection = true
                });

                // 检查数据库名是否为空
                db.Ado.ExecuteCommand("SELECT 1");

                // 检查数据库是否存在
                var dbResult = db.Ado.SqlQuery<string>(
                    "SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = @dbName",
                    new { dbName = mySqlConfig.Database }
                );

                if (dbResult == null || dbResult.Count == 0)
                {
                    log.Warn("Database does not exist.");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "数据库不存在。");
                    });
                    return;
                }

                log.Info("连接成功，数据库及表（如指定）存在。");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "连接成功");
                });

            }
            catch (SqlSugarException ex)
            {
                log.Error(ex);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // SqlSugarException 没有 MySqlException 的 Number 属性，只能提示通用信息
                    MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库连接失败，SqlSugar异常：{ex.Message}");
                });
            }
            catch (Exception ex)
            {
                log.Error(ex);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接发生未知错误！");
                });
            }
        }

        public void Close()
        {
            MySqlConnection.Close();
        }


        public List<string> GetTableNames()
        {
            var dbName = MySqlSetting.Instance.MySqlConfig.Database;
            var sql = @"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @dbName AND TABLE_TYPE = 'BASE TABLE'";
            var result = DB.Ado.SqlQuery<string>(sql, new { dbName });
            return result;
        }

        public List<string> GetFilteredResourceTableNames()
        {
            var tableNames = GetTableNames();
            var prefixes = new[] { "t_scgd_sys_config", "t_scgd_sys_globle_cfg", "t_scgd_sys_mqtt_cfg", "t_scgd_rc", "t_scgd_sys_version" };
            return tableNames
                .Where(name => !prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.CurrentCulture)))
                .ToList();
        }


        public int ExecuteNonQuery(string sql, Dictionary<string, object>? param = null)
        {
            try
            {
                return param == null
                    ? DB.Ado.ExecuteCommand(sql)
                    : DB.Ado.ExecuteCommand(sql, param);
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return -1;
            }
        }
        public int BatchExecuteNonQuery(string sqlBatch)
        {
            var statements = sqlBatch.Split(';', StringSplitOptions.RemoveEmptyEntries);
            int totalCount = 0;
            try
            {
                DB.Ado.BeginTran();
                foreach (var sql in statements)
                {
                    var trimmedSql = sql.Trim();
                    if (string.IsNullOrEmpty(trimmedSql))
                        continue;
                    int count = DB.Ado.ExecuteCommand(trimmedSql);
                    totalCount += count;
                    log.Info($"SQL执行成功。受影响的行数: {count} 执行的SQL语句: {trimmedSql}");
                }
                DB.Ado.CommitTran();
            }
            catch (Exception ex)
            {
                DB.Ado.RollbackTran();
                log.Error($"SQL批量执行失败: {ex.Message}");
            }
            log.Info($"总共受影响的行数: {totalCount}");
            return totalCount;
        }



        public void Dispose()
        {
            MySqlConnection?.Dispose();
            DB?.Dispose();
            GC.SuppressFinalize(this);
        }

    }
}
