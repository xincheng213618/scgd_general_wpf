﻿using ColorVision.MQTT;
using ColorVision.MVVM;
using ColorVision.MySql;
using ColorVision.RC;
using ColorVision.SettingUp;
using ColorVision.Solution;
using ColorVision.Templates;
using ColorVision.Update;
using ColorVision.User;
using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace ColorVision
{
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
            SolutionConfig = new SolutionConfig();
            SystemMonitorSetting = new SystemMonitorSetting();
            SystemMonitorLazy = new Lazy<SystemMonitor>(() => SystemMonitor.GetInstance());
            TemplateControlLazy = new Lazy<TemplateControl>(() => TemplateControl.GetInstance());


            MQTTSetting = new MQTTSetting();
            MQTTConfig = new MQTTConfig();
            MQTTConfigs = new ObservableCollection<MQTTConfig>();
            MQTTControlLazy = new Lazy<MQTTControl>(() => MQTTControl.GetInstance());


            MySqlConfig = new MySqlConfig();
            MySqlConfigs = new ObservableCollection<MySqlConfig>();
            MySqlControlLazy = new Lazy<MySqlControl>(() => MySqlControl.GetInstance());

            RcServiceConfig = new RCServiceConfig();
            RcServiceConfigs = new ObservableCollection<RCServiceConfig>();
            RcServiceControlLazy = new Lazy<RCService>(() => RCService.GetInstance());

            VideoConfig = new LocalVideoConfig();
        }
        [JsonIgnore]
        public AutoUpdater AutoUpdater { get;} = AutoUpdater.GetInstance();

        public static string Version { get => System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version?.ToString() ?? "1.0"; } 
        public bool IsUseMySql { get => _IsUseMySql; set { _IsUseMySql = value; NotifyPropertyChanged(); UseMySqlChanged?.Invoke(value); } }
        private bool _IsUseMySql = true;

        public event UseMySqlHandler UseMySqlChanged;

        /// <summary>
        /// MQTT
        /// </summary>
        public bool IsUseMQTT { get => _IsUseMQTT; set { _IsUseMQTT = value; NotifyPropertyChanged(); } }
        private bool _IsUseMQTT = true;
        /// <summary>
        /// 注册中心
        /// </summary>
        public bool IsUseRCService { get => _IsUseRCService; set { _IsUseRCService = value; NotifyPropertyChanged(); } }
        private bool _IsUseRCService = true;


        public SoftwareSetting SoftwareSetting { get; set; }
        public LocalVideoConfig VideoConfig { get; set; }


        [JsonIgnore]
        readonly Lazy<SystemMonitor> SystemMonitorLazy;
        [JsonIgnore]
        public SystemMonitor SystemMonitor { get => SystemMonitorLazy.Value; }

        public SystemMonitorSetting SystemMonitorSetting { get; set; }

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

        public SolutionConfig SolutionConfig { get; set; }

        [JsonIgnore]
        public static SolutionManager SolutionManager { get => SolutionManager.GetInstance(); }

        public SolutionSetting SolutionSetting { get; set; } = new SolutionSetting();


        public RCServiceConfig RcServiceConfig { get; set; }
        public ObservableCollection<RCServiceConfig> RcServiceConfigs { get; set; }

        [JsonIgnore]
        readonly Lazy<RCService> RcServiceControlLazy;
        [JsonIgnore]
        public RCService RCService { get => RcServiceControlLazy.Value; }
    }

    public class UserManager
    {
        public static UserManager Current { get; set; } = new UserManager();


        public UserConfig UserConfig { get; set; }
    }
}
