using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.Services;
using ColorVision.Services.Msg;
using ColorVision.Services.RC;
using ColorVision.Solution;
using ColorVision.UI;
using ColorVision.Update;
using ColorVision.UserSpace;

namespace ColorVision.Settings
{
    /// <summary>
    /// 软件配置
    /// </summary>
    public class SoftwareConfig : ViewModelBase
    {
        public static MySqlSetting MySqlSetting => ConfigHandler.GetInstance().GetRequiredService<MySqlSetting>();  

        public bool IsAutoRun { get => Tool.IsAutoRun(GlobalConst.AutoRunName, GlobalConst.AutoRunRegPath); set { Tool.SetAutoRun(value, GlobalConst.AutoRunName, GlobalConst.AutoRunRegPath); NotifyPropertyChanged(); } }

        public static AutoUpdateConfig AutoUpdateConfig => AutoUpdateConfig.Instance;

        public static SoftwareSetting SoftwareSetting => SoftwareSetting.Instance;

        public static SystemMonitorSetting SystemMonitorSetting => ConfigHandler.GetInstance().GetRequiredService<SystemMonitorSetting>();

        public static RCSetting RCSetting => RCSetting.Instance;

        public static ServicesConfig ServicesSetting => ServicesConfig.Instance;

        public static MQTTSetting MQTTSetting => MQTTSetting.Instance;

        public static MsgConfig MsgConfig => MsgConfig.Instance;
        public static SystemMonitor SystemMonitor => SystemMonitor.GetInstance();

        public static UserConfig UserConfig => UserConfig.Instance;

        public static SolutionSetting SolutionSetting  =>ConfigHandler.GetInstance().GetRequiredService<SolutionSetting>();
    }


}
