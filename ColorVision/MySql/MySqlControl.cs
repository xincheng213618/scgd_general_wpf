using ColorVision.MVVM;
using MySql.Data.MySqlClient;
using System;
using log4net;
using System.Windows;
using System.Threading.Tasks;

namespace ColorVision.MySql
{

    public class MySqlControl: ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MySqlControl));
        private static MySqlControl _instance;
        private static readonly object _locker = new();
        public static MySqlControl GetInstance() { lock (_locker) { return _instance ??= new MySqlControl(); } }
        public MySqlConnection MySqlConnection { get; set; }

        public SoftwareConfig SoftwareConfig { get; set; }

        public MySqlConfig Config { get => SoftwareConfig.MySqlConfig; }


        public MySqlControl()
        {
            SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;
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
                Application.Current.Dispatcher.Invoke(() => {
                    MySqlConnectChanged?.Invoke(this, new EventArgs());
                });
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
        public string GetConnectionString() => GetConnectionString(Config);
        public static string GetConnectionString(MySqlConfig MySqlConfig,int timeout =3 )
        {
            string connStr = $"server={MySqlConfig.Host};port={MySqlConfig.Port};uid={MySqlConfig.UserName};pwd={MySqlConfig.UserPwd};database={MySqlConfig.Database};charset=utf8;Connect Timeout={timeout}";
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

    }
}
