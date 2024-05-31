using ColorVision.Common.MVVM;
using log4net;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.MySql
{

    public class MySqlControl: ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MySqlControl));
        private static MySqlControl _instance;
        private static readonly object _locker = new();
        public static MySqlControl GetInstance() { lock (_locker) { return _instance ??= new MySqlControl(); } }

        public MySqlConnection MySqlConnection { get; set; }

        public static MySqlConfig Config => MySqlSetting.Instance.MySqlConfig; 

        public MySqlControl()
        {
        }

        public event EventHandler MySqlConnectChanged;

        public bool IsConnect { get => _IsConnect; private set { _IsConnect = value; NotifyPropertyChanged(); } }
        private bool _IsConnect;

        public Task<bool> Connect()
        {
            string connStr = GetConnectionString(Config);
            try
            {
                IsConnect = false;
                log.Info($"正在连接数据库:{connStr}");
                MySqlConnection = new MySqlConnection() { ConnectionString = connStr  };
                MySqlConnection.Open();
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
        public static string GetConnectionString() => GetConnectionString(Config);

        public static string GetConnectionString(MySqlConfig MySqlConfig,int timeout = 3 )
        {
            string connStr = $"server={MySqlConfig.Host};port={MySqlConfig.Port};uid={MySqlConfig.UserName};pwd={MySqlConfig.UserPwd};database={MySqlConfig.Database};charset=utf8;Connect Timeout={timeout};SSL Mode =None";
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
                return true;
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
        }

    }
}
