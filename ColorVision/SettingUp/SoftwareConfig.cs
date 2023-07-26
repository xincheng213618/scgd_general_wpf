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

        public double Width { get; set; }
        public double Height { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public int WindowState { get; set; }

        public bool IsOpenStatusBar { get => _IsOpenStatusBar; set { _IsOpenStatusBar = value; NotifyPropertyChanged(); } }
        private bool _IsOpenStatusBar = true;
        public bool IsOpenSidebar { get => _IsOpenSidebar; set { _IsOpenSidebar = value; NotifyPropertyChanged(); } }
        private bool _IsOpenSidebar = true;


        public string LogLevel
        {
            get => _LogLevel; set
            {
                _LogLevel = value;
                NotifyPropertyChanged();
                Level level = Level.All;
                switch (LogLevel)
                {
                    case "info":
                        level = Level.Info;
                        break;
                    case "debug":
                        level = Level.Debug;
                        break;
                    case "warn":
                        level = Level.Warn;
                        break;
                    case "error":
                        level = Level.Error;
                        break;
                    default:
                        level = Level.All;
                        break;
                }

                var hierarchy = (Hierarchy)LogManager.GetRepository();
                hierarchy.Root.Level = level;
                log4net.Config.BasicConfigurator.Configure(hierarchy);
                log.Info("更新log4Net" + value);
            }
        }
        private string _LogLevel = GlobalConst.LogLevel[0];
    }

    public delegate void UseMySqlHandler(bool IsUseMySql);

    /// <summary>
    /// 软件配置
    /// </summary>
    public class SoftwareConfig : ViewModelBase
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


        public string Version { get; set; } = "1.0";
        public bool IsUseMySql { get => _IsUseMySql; set { _IsUseMySql = value; NotifyPropertyChanged(); UseMySqlChanged?.Invoke(value); } }
        private bool _IsUseMySql = true;

        public event UseMySqlHandler UseMySqlChanged;


        public bool IsUseMQTT { get => _IsUseMQTT; set { _IsUseMQTT = value; NotifyPropertyChanged(); } } 
        private bool _IsUseMQTT = true;




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
