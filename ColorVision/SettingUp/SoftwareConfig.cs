using ColorVision.MQTT;
using ColorVision.MVVM;
using ColorVision.MySql;
using ColorVision.Template;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
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
            UserConfig = new UserConfig();
            ProjectConfig = new ProjectConfig();
            PerformanceConfig = new PerformanceConfig();
            PerformanceControlLazy = new Lazy<PerformanceControl>(() => PerformanceControl.GetInstance());
            TemplateControlLazy = new Lazy<TemplateControl>(() => TemplateControl.GetInstance());


            MQTTSetting = new MQTTSetting();
            MQTTConfig = new MQTTConfig();
            MQTTConfigs = new ObservableCollection<MQTTConfig>();
            MQTTControlLazy = new Lazy<MQTTControl>(() => MQTTControl.GetInstance());


            MySqlConfig = new MySqlConfig();
            MySqlConfigs = new ObservableCollection<MySqlConfig>();
            MySqlControlLazy = new Lazy<MySqlControl>(() => MySqlControl.GetInstance());

            CameraVideoConfig = new CameraVideoConfig();
            CameraVideoConfigs = new ObservableCollection<CameraVideoConfig>();


        }


        public string Version { get; set; } = "0.0";

        public bool IsUseMySql { get; set; } = true;


        public bool IsUseMQTT { get; set; } = true;

        public bool IsOpenStatusBar { get; set; }

        public bool IsOpenSidebar { get; set; } = true;


        [JsonIgnore]
        readonly Lazy<PerformanceControl> PerformanceControlLazy;
        [JsonIgnore]
        public PerformanceControl PerformanceControl { get => PerformanceControlLazy.Value; }

        public PerformanceConfig PerformanceConfig { get; set; }

        [JsonIgnore]
        readonly Lazy<TemplateControl> TemplateControlLazy;
        [JsonIgnore]
        public TemplateControl TemplateControl { get => TemplateControlLazy.Value; }

        public MQTTSetting MQTTSetting { get; set; }
        public MQTTConfig MQTTConfig { get; set; }

        public ObservableCollection<MQTTConfig> MQTTConfigs { get; set; } 
        [JsonIgnore]
        readonly Lazy<MQTTControl> MQTTControlLazy;
        [JsonIgnore]
        public MQTTControl MQTTControl { get => MQTTControlLazy.Value; }


        /// <summary>
        /// MySQL配置
        /// </summary>
        public MySqlConfig MySqlConfig { get; set; }
        public ObservableCollection<MySqlConfig> MySqlConfigs { get; set; }

        [JsonIgnore]
        readonly Lazy<MySqlControl> MySqlControlLazy;
        [JsonIgnore]
        public MySqlControl MySqlControl { get => MySqlControlLazy.Value; }



        public UserConfig UserConfig { get; set; }

        public ProjectConfig ProjectConfig { get; set; }

        public CameraVideoConfig CameraVideoConfig { get; set; }


        public ObservableCollection<CameraVideoConfig> CameraVideoConfigs { get; set; }

    }
}
