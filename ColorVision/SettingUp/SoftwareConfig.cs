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
            UserConfig = new UserConfig();
            ProjectConfig = new ProjectConfig();
        }

        public string Version { get; set; } = "0.0";

        public bool IsUseMySql { get; set; } = true;


        /// <summary>
        /// MQTT配置
        /// </summary>
        public MQTTConfig MQTTConfig { get; set; }

        /// <summary>
        /// MySQL配置
        /// </summary>
        public MySqlConfig MySqlConfig { get; set; }

        public UserConfig UserConfig { get; set; }

        public ProjectConfig ProjectConfig { get; set; }

    }
}
