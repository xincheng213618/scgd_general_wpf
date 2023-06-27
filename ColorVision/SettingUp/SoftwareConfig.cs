using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.SettingUp
{
    /// <summary>
    /// 软件配置
    /// </summary>
    public class SoftwareConfig
    {
        public SoftwareConfig()
        {
            MQTTSetting = new MQTTConfig();
            MySQLConfig = new MySQLConfig();
        }

        public string Version { get; set; } = "0.0";

        /// <summary>
        /// MQTT配置
        /// </summary>
        public MQTTConfig MQTTSetting { get; set; }

        /// <summary>
        /// MySQL配置
        /// </summary>
        public MySQLConfig MySQLConfig { get; set; }


    }
}
