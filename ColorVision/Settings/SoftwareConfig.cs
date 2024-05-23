using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services;
using ColorVision.Solution;
using ColorVision.UI;
using ColorVision.Update;

namespace ColorVision.Settings
{
    /// <summary>
    /// 软件配置
    /// </summary>
    public class SoftwareConfig : ViewModelBase
    {
        public bool IsAutoRun { get => Tool.IsAutoRun(GlobalConst.AutoRunName, GlobalConst.AutoRunRegPath); set { Tool.SetAutoRun(value, GlobalConst.AutoRunName, GlobalConst.AutoRunRegPath); NotifyPropertyChanged(); } }
        public static AutoUpdateConfig AutoUpdateConfig => AutoUpdateConfig.Instance;
        public static SoftwareSetting SoftwareSetting => SoftwareSetting.Instance;
        public static ServicesConfig ServicesSetting => ServicesConfig.Instance;
        public static SolutionSetting SolutionSetting  =>ConfigHandler.GetInstance().GetRequiredService<SolutionSetting>();
    }


}
