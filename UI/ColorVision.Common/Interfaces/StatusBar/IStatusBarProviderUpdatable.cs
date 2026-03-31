using System;

namespace ColorVision.UI
{
    /// <summary>
    /// 可动态刷新的状态栏提供者。
    /// 当内部状态变化时触发 StatusBarItemsChanged，StatusBarManager 会重新获取项目列表。
    /// </summary>
    public interface IStatusBarProviderUpdatable : IStatusBarProvider
    {
        event EventHandler StatusBarItemsChanged;
    }
}
