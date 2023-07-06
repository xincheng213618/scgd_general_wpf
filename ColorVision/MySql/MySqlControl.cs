using ColorVision.MQTT;
using ColorVision.MVVM;
using ColorVision.SettingUp;
using MQTTnet.Client;
using MQTTnet;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

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

        public MySqlControl()
        {
            SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;
            Task.Run(() => Open());
        }

        public bool IsConnect { get => _IsConnect; private set { _IsConnect = value; NotifyPropertyChanged(); } }
        private bool _IsConnect;

        public string ConnectSign { get => _ConnectSign; private set { _ConnectSign = value; NotifyPropertyChanged(); } }
        private string _ConnectSign = "未连接";


        public string GetCurConnectionString()
        {
            string connStr = GetConnectionString(SoftwareConfig.MySqlConfig);
            return connStr;
        }

        public bool Open()
        {
            string connStr = GetConnectionString(SoftwareConfig.MySqlConfig);
            try
            {
                log.Info($"数据库连接信息:{connStr}");
                MySqlConnection = new MySqlConnection() { ConnectionString = connStr  };
                MySqlConnection.Open();

                IsConnect = true;
                ConnectSign = "已连接";
                return true;
            }
            catch (Exception ex)
            {
                IsConnect = false;
                ConnectSign = "未连接";
                log.Error(ex);
                return false;
            }
        }

        public static string GetConnectionString(MySqlConfig MySqlConfig)
        {
            string connStr = $"server={MySqlConfig.Host};uid={MySqlConfig.UserName};pwd={MySqlConfig.UserPwd};database={MySqlConfig.Database};Connect Timeout=3";
            return connStr;
        }

        public static bool TestConnect(MySqlConfig MySqlConfig)
        {
            MySqlConnection MySqlConnection;
            string connStr = GetConnectionString(MySqlConfig);
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
