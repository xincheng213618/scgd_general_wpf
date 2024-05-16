using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.Services.Msg;
using ColorVision.Services.RC;
using ColorVision.Solution;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Views;
using ColorVision.Update;
using ColorVision.UserSpace;
using ColorVision.Wizards;

namespace ColorVision.Settings
{
    /// <summary>
    /// 软件配置
    /// </summary>
    public class SoftwareConfig : ViewModelBase,IConfig
    {
        public static SoftwareConfig Instance => ConfigHandler1.GetInstance().GetRequiredService<SoftwareConfig>();

        public static MySqlSetting MySqlSetting => ConfigHandler1.GetInstance().GetRequiredService<MySqlSetting>();  

        public bool IsAutoRun { get => Tool.IsAutoRun(GlobalConst.AutoRunName, GlobalConst.AutoRunRegPath); set { Tool.SetAutoRun(value, GlobalConst.AutoRunName, GlobalConst.AutoRunRegPath); NotifyPropertyChanged(); } }

        public static MainWindowConfig MainWindowConfig => MainWindowConfig.Instance;

        public static ThemeConfig ThemeConfig => ThemeConfig.Instance;

        public static WizardConfig WizardConfig => WizardConfig.Instance;

        public static AutoUpdateConfig AutoUpdateConfig => AutoUpdateConfig.Instance;

        public static ViewConfig ViewConfig => ViewConfig.Instance;

        public string? Version { get => _Version; set { _Version = value; NotifyPropertyChanged(); } }
        private string? _Version = string.Empty;

        public SoftwareSetting SoftwareSetting { get; set; } = new SoftwareSetting();

        public static SystemMonitorSetting SystemMonitorSetting => ConfigHandler1.GetInstance().GetRequiredService<SystemMonitorSetting>();

        public static RCSetting RCSetting => RCSetting.Instance;

        public static MQTTSetting MQTTSetting => MQTTSetting.Instance;
        public static MsgConfig MsgConfig => MsgConfig.Instance;
        public static SystemMonitor SystemMonitor => SystemMonitor.GetInstance();

        public static UserConfig UserConfig => UserConfig.Instance;

        public static SolutionSetting SolutionSetting  =>ConfigHandler1.GetInstance().GetRequiredService<SolutionSetting>();
    }


}
