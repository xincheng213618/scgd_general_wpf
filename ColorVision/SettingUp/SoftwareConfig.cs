using ColorVision.MQTT;
using ColorVision.MVVM;
using ColorVision.MySql;
using ColorVision.Template;
using log4net;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
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
    public class SoftwareSetting :ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SoftwareSetting));

        public SoftwareSetting()
        {
        }

        public bool IsRestoreWindow { get; set; }
        private bool _IsRestoreWindow { get; set; }

        public double Width { get; set; }
        public double Height { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public int WindowState { get; set; }



        //public Level Level { get => _Level; set { 
        //        _Level = value;
        //        NotifyPropertyChanged();

        //        var hierarchy = (Hierarchy)LogManager.GetRepository();

        //        // 设置日志级别
        //        hierarchy.Root.Level = value;

        //        // 配置并激活log4net
        //        log4net.Config.BasicConfigurator.Configure(hierarchy);
        //        log.Info("更新log4Net" + value);
        //    } }
        //private Level _Level = Level.All;
    }


    /// <summary>
    /// 软件配置
    /// </summary>
    public class SoftwareConfig
    {
        public SoftwareConfig()
        {
            SoftwareSetting = new SoftwareSetting();

            UserConfig = new UserConfig();
            ProjectConfig = new ProjectConfig();
            PerformancSetting = new PerformancSetting();
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

        public SoftwareSetting SoftwareSetting { get; set; }


        [JsonIgnore]
        readonly Lazy<PerformanceControl> PerformanceControlLazy;
        [JsonIgnore]
        public PerformanceControl PerformanceControl { get => PerformanceControlLazy.Value; }

        public PerformancSetting PerformancSetting { get; set; }

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
