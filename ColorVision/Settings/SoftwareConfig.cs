using ColorVision.Common.MVVM;
using ColorVision.Solution;
using ColorVision.UI;

namespace ColorVision.Settings
{
    /// <summary>
    /// 软件配置
    /// </summary>
    public class SoftwareConfig : ViewModelBase
    {
        public static SoftwareSetting SoftwareSetting => SoftwareSetting.Instance;
        public static SolutionSetting SolutionSetting  =>ConfigHandler.GetInstance().GetRequiredService<SolutionSetting>();
    }


}
