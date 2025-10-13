using System.Collections.ObjectModel;

namespace ColorVision.UI
{
    /// <summary>
    /// 状态栏信息提供者接口
    /// 控件可以实现此接口来提供动态的状态栏信息
    /// </summary>
    public interface IStatusBarInfoProvider
    {
        /// <summary>
        /// 获取状态栏信息项集合
        /// 该集合应该是可观察的，以便状态栏能够自动更新
        /// </summary>
        /// <returns>状态栏信息项的可观察集合</returns>
        ObservableCollection<StatusBarInfoItem> GetStatusBarInfo();
    }
}
