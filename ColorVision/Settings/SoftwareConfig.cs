using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.Services.RC;
using ColorVision.Solution;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Views;
using ColorVision.Update;
using ColorVision.UserSpace;
using ColorVision.Wizards;
using System.Collections.ObjectModel;

namespace ColorVision.Settings
{
    public delegate void UseMQTTHandler(bool IsUseMQTT);
    public delegate void UseRcServicelHandler(bool IsUseRcServicel);

    /// <summary>
    /// 软件配置
    /// </summary>
    public class SoftwareConfig : ViewModelBase
    {
        public SoftwareConfig()
        {
            SoftwareSetting = new SoftwareSetting();
            UserConfig = new UserConfig();

            MQTTSetting = new MQTTSetting();

            MQTTConfig = new MQTTConfig();
            MQTTConfigs = new ObservableCollection<MQTTConfig>();

            RcServiceConfig = new RCServiceConfig();
            RcServiceConfigs = new ObservableCollection<RCServiceConfig>();
        }
        public static MySqlSetting MySqlSetting => ConfigHandler1.GetInstance().GetRequiredService<MySqlSetting>();  

        public bool IsAutoRun { get => Tool.IsAutoRun(GlobalConst.AutoRunName, GlobalConst.AutoRunRegPath); set { Tool.SetAutoRun(value, GlobalConst.AutoRunName, GlobalConst.AutoRunRegPath); NotifyPropertyChanged(); } }

        public static MainWindowConfig MainWindowConfig => MainWindowConfig.Instance;

        public static ThemeConfig ThemeConfig => ThemeConfig.Instance;

        public static WizardConfig WizardConfig => WizardConfig.Instance;

        public static AutoUpdateConfig AutoUpdateConfig => AutoUpdateConfig.Instance;

        public ViewConfig ViewConfig { get; } = ViewConfig.GetInstance();

        public string? Version { get => _Version; set { _Version = value; NotifyPropertyChanged(); } }
        private string? _Version = string.Empty;


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

        public static SystemMonitorSetting SystemMonitorSetting => ConfigHandler1.GetInstance().GetRequiredService<SystemMonitorSetting>();


        public MQTTSetting MQTTSetting { get; set; }
        public MQTTConfig MQTTConfig { get; set; }

        public ObservableCollection<MQTTConfig> MQTTConfigs { get; set; } 


        public UserConfig UserConfig { get; set; }

        public SolutionSetting SolutionSetting { get; set; } = new SolutionSetting();


        public RCServiceConfig RcServiceConfig { get; set; }
        public ObservableCollection<RCServiceConfig> RcServiceConfigs { get; set; }
    }


}
