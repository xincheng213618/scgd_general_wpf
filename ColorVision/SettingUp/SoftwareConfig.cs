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
            MQTTConfig = new MQTTConfig();
            MySqlConfig = new MySqlConfig();
        }

        public string Version { get; set; } = "0.0";

        /// <summary>
        /// MQTT配置
        /// </summary>
        public MQTTConfig MQTTConfig { get; set; }

        /// <summary>
        /// MySQL配置
        /// </summary>
        public MySqlConfig MySqlConfig { get; set; }


    }
}
