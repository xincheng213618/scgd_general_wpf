using ColorVision.Common.MVVM;
using ColorVision.Database.Properties;
using log4net;
using MySqlConnector;
using SqlSugar;
using System;
using System.IO;
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


        public MySqlControl()
        {
            StaticConfig.BulkCopy_MySqlCsvPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "bulkcopyfiles");
        }

        public event EventHandler MySqlConnectChanged;

        public bool IsConnect
        {
            get => _IsConnect;
            private set
            {
                _IsConnect = value;
                if (Application.Current?.Dispatcher.CheckAccess() == true)
                {
                    OnPropertyChanged();
                    MySqlConnectChanged?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        OnPropertyChanged();
                        MySqlConnectChanged?.Invoke(this, EventArgs.Empty);
                    });
                }
            }
        }
        private bool _IsConnect;

        private static readonly char[] separator = new[] { ';' };

        public Task<bool> Connect()
        {
            string connStr = GetConnectionString(Config);
            try
            {
                IsConnect = false;
                var newConn = new MySqlConnection() { ConnectionString = connStr };
                newConn.Open();

                log.Info($"数据库连接成功:{GetConnectionSummary(Config)}");
                using var  _DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = GetConnectionString(Config), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

                // 检查数据库名是否为空
                // 检查当前 local_infile 的值
                int localInfile = _DB.Ado.GetInt("SELECT @@global.local_infile;");

                if (localInfile == 0)
                {
                    // 不支持则设置为 1
                    _DB.Ado.ExecuteCommand("SET GLOBAL local_infile = 1;");
                    log.Info("local_infile 已设置为 1");
                }
                else
                {
                    log.Info("local_infile 已经支持");
                }
                IsConnect = true;
                newConn.Close();
                newConn.Dispose();

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
                log.Error($"数据库连接失败: {detailMsg}. 连接: {GetConnectionSummary(Config)}");
                log.Error(ex);
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                IsConnect = false;
                log.Error($"数据库连接发生未知异常: {ex.Message}. 连接: {GetConnectionSummary(Config)}");
                log.Error(ex);
                return Task.FromResult(false);
            }
        }
        public static SqlSugarClient CreateDbClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = GetConnectionString(),
                DbType = DbType.MySql,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
        }

        public static IDatabaseBrowserProvider CreateBrowserProvider()
        {
            return new MySqlDatabaseBrowserProvider(() => Config);
        }


        public static string GetConnectionString() => GetConnectionString(Config);

        public static string GetConnectionString(MySqlConfig MySqlConfig,int timeout = 1)
        {
            return GetConnectionString(MySqlConfig, timeout, MySqlConfig.Database);
        }

        public static string GetConnectionString(MySqlConfig MySqlConfig, int timeout, string? databaseName)
        {
            string connStr = $"server={MySqlConfig.Host};port={MySqlConfig.Port};uid={MySqlConfig.UserName};pwd={MySqlConfig.UserPwd};database={databaseName ?? string.Empty};charset=utf8;Connect Timeout={timeout};SSL Mode =None;Pooling=true;AllowLoadLocalInfile=True";
            return connStr;
        }

        private static string GetConnectionSummary(MySqlConfig mySqlConfig)
        {
            string database = string.IsNullOrWhiteSpace(mySqlConfig.Database) ? "<empty>" : mySqlConfig.Database;
            string user = string.IsNullOrWhiteSpace(mySqlConfig.UserName) ? "<empty>" : "***";
            return $"server={mySqlConfig.Host};port={mySqlConfig.Port};uid={user};database={database}";
        }

        public static void TestConnect(MySqlConfig MySqlConfig)
        {
            string connStr = GetConnectionString(MySqlConfig, 2);
            try
            {
                log.Info($"Test数据库连接信息:{GetConnectionSummary(MySqlConfig)}");
                using (var mySqlConnection = new MySqlConnection(connStr))
                {
                    mySqlConnection.Open();

                    if (string.IsNullOrEmpty(MySqlConfig.Database))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.DB_DbNameEmpty);
                        });
                    }

                    // 查询数据库是否存在
                    string query = $"SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = @dbName";
                    using (var command = new MySqlCommand(query, mySqlConnection))
                    {
                        command.Parameters.AddWithValue("@dbName", MySqlConfig.Database);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                log.Info("Database exists.");
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.DB_ConnectSuccess);
                                });
                            }
                            else
                            {
                                log.Warn("Database does not exist.");
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.DB_DbNotExist);
                                });
                            }
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                log.Error(ex);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    switch (ex.Number)
                    {
                        case 1045:
                            MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.DB_AuthError);
                            break;
                        case 1049:
                            MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.DB_SpecifiedDbNotExist);
                            break;
                        case 2003:
                            MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.DB_ConnectFailed);
                            break;
                        default:
                            MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库连接失败，错误码：{ex.Number}");
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                log.Error(ex);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.DB_UnknownError);
                });
            }
        }

        public static  int BatchExecuteNonQuery(string sqlBatch)
        {
            var statements = sqlBatch.Split(';', StringSplitOptions.RemoveEmptyEntries);
            int totalCount = 0;
            using var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
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
            GC.SuppressFinalize(this);
        }

    }
}
