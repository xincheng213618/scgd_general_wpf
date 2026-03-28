namespace ColorVision.UI.Views
{
    /// <summary>
    /// 全局 IViewManager 访问点。
    /// 应用启动时设置 Current，所有需要 IViewManager 的代码通过此类获取。
    /// 默认回退到 ViewGridManager.GetInstance()（兼容现有代码）。
    /// </summary>
    public static class ViewManagerProvider
    {
        private static IViewManager? _current;

        /// <summary>
        /// 当前活动的视图管理器。
        /// 在 MainWindow 初始化时设置（ViewGridManager 或 DockViewManager）。
        /// 若未设置，回退到 ViewGridManager 单例。
        /// </summary>
        public static IViewManager Current
        {
            get => _current ?? ViewGridManager.GetInstance();
            set => _current = value;
        }
    }
}
