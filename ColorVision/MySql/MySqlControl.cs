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

namespace ColorVision.MySql
{
    public class MySqlControl: ViewModelBase
    {
        private static MySqlControl _instance;
        private static readonly object _locker = new();
        public static MySqlControl GetInstance() { lock (_locker) { return _instance ??= new MySqlControl(); } }
        public MySqlConfig MySqlConfig { get; set; }
        public MySqlConnection MySqlConnection { get; set; }

        public MySqlControl()
        {
            MySqlConfig = GlobalSetting.GetInstance().SoftwareConfig.MySqlConfig;
            Task.Run(() => Open());
        }

        public bool IsConnect { get => _IsConnect; private set { _IsConnect = value; NotifyPropertyChanged(); } }
        private bool _IsConnect;

        public string ConnectSign { get => _ConnectSign; private set { _ConnectSign = value; NotifyPropertyChanged(); } }
        private string _ConnectSign = "未连接";



        public bool Open()
        {
            string connStr = $"server={MySqlConfig.Host};uid={MySqlConfig.UserName};pwd={MySqlConfig.UserPwd};database={MySqlConfig.Database}";
            try
            {
                Log.LogWrite($"数据库连接信息:{connStr}");
                MySqlConnection = new MySqlConnection() { ConnectionString = connStr  };
                MySqlConnection.Open();

                IsConnect = true;
                ConnectSign = "已连接";
                return true;
            }
            catch (Exception ex)
            {
                IsConnect = false;
                Log.LogException(ex);
                return false;
            }
        }

        public static bool TestConnect(MySqlConfig MySqlConfig)
        {
            string connStr = $"server={MySqlConfig.Host};uid={MySqlConfig.UserName};pwd={MySqlConfig.UserPwd};database={MySqlConfig.Database};;Connect Timeout=3";
            MySqlConnection MySqlConnection;
            try
            {
                Log.LogWrite($"Test数据库连接信息:{connStr}");
                MySqlConnection = new MySqlConnection() { ConnectionString = connStr };
                MySqlConnection.Open();
                return true;
            }
            catch (Exception ex)
            {
                Log.LogException(ex);
                return false;
            }
        }

        public void Close()
        {
            MySqlConnection.Close();
        }

    }
}
