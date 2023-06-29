using ColorVision.MQTT;
using ColorVision.SettingUp;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql
{
    public class MySqlControl
    {
        private static MySqlControl _instance;
        private static readonly object _locker = new();
        public static MySqlControl GetInstance() { lock (_locker) { return _instance ??= new MySqlControl(); } }
        public MySqlConfig MySqlConfig { get; set; }
        public MySqlConnection MySqlConnection { get; set; }

        public MySqlControl()
        {
            MySqlConfig = GlobalSetting.GetInstance().SoftwareConfig.MySqlConfig;
        }

        public bool Open()
        {
            string connStr = $"server={MySqlConfig.Host};uid={MySqlConfig.UserName};pwd={MySqlConfig.UserPwd};database={MySqlConfig.Database}";
            try
            {
                Log.LogWrite($"数据库连接信息:{connStr}");
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
