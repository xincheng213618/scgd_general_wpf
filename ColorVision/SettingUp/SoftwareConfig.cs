using ColorVision.MQTT;
using ColorVision.MVVM;
using ColorVision.MySql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ColorVision.SettingUp
{
    /// <summary>
    /// 软件配置
    /// </summary>
    public class SoftwareConfig: ViewModelBase
    {
        public SoftwareConfig()
        {
            MQTTConfig = new MQTTConfig();
            MySqlConfig = new MySqlConfig();
            UserConfig = new UserConfig();
            ProjectConfig = new ProjectConfig();
            MQTTControlLazy = new Lazy<MQTTControl>(() => MQTTControl.GetInstance());
            MySqlControlLazy = new Lazy<MySqlControl>(() => MySqlControl.GetInstance());
        }

        public string Version { get; set; } = "0.0";

        public bool IsUseMySql { get => _IsUseMySql; set { _IsUseMySql = value; NotifyPropertyChanged();} }
        private bool _IsUseMySql = true;


        public bool IsUseMQTT { get; set; } = true;

        public bool IsOpenStatusBar { get; set; } = true;
        public bool IsOpenSidebar { get; set; } = true;


        /// <summary>
        /// MQTT配置
        /// </summary>
        public MQTTConfig MQTTConfig { get; set; }

        [JsonIgnore]
        readonly Lazy<MQTTControl> MQTTControlLazy;
        [JsonIgnore]
        public MQTTControl MQTTControl { get => MQTTControlLazy.Value; }


        /// <summary>
        /// MySQL配置
        /// </summary>
        public MySqlConfig MySqlConfig { get; set; }
        [JsonIgnore]
        readonly Lazy<MySqlControl> MySqlControlLazy;
        [JsonIgnore]
        public MySqlControl MySqlControl { get => MySqlControlLazy.Value; }


        public UserConfig UserConfig { get; set; }

        public ProjectConfig ProjectConfig { get; set; }

    }
}
