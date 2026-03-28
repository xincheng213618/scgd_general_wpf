using System.Windows.Controls;

namespace ColorVision.UI.Views
{
    /// <summary>
    /// 视图管理器（单例）。
    /// 管理所有视图控件的注册和激活。
    /// 控件无需实现 IView，直接传入 UserControl 即可。
    /// AvalonDock 的文档创建由 ActiveViewHandler 回调完成（在主窗口初始化时设置）。
    /// </summary>
    public class DockViewManager
    {
        private static DockViewManager _instance;
        private static readonly object _locker = new();
        public static DockViewManager GetInstance() { lock (_locker) { return _instance ??= new DockViewManager(); } }

        /// <summary>
        /// 已注册的视图控件列表
        /// </summary>
        public List<Control> Views { get; } = new();

        /// <summary>
        /// 视图控件 → 标题的映射（由 AddViewConfig 设置）
        /// </summary>
        public Dictionary<Control, string> ViewTitles { get; } = new();

        /// <summary>
        /// 上一次激活的视图控件
        /// </summary>
        public Control? LastActiveView { get; set; }

        /// <summary>
        /// 激活视图的回调（由主窗口初始化时设置，实现 AvalonDock 文档创建/切换）
        /// </summary>
        public Action<Control>? ActiveViewHandler { get; set; }

        /// <summary>
        /// 将所有已注册视图创建为文档标签页的回调
        /// </summary>
        public Action? ShowAllViewsHandler { get; set; }

        private DockViewManager()
        {
        }

        /// <summary>
        /// 注册视图控件
        /// </summary>
        public int AddView(Control control)
        {
            if (control == null) return -1;
            if (Views.Contains(control)) return Views.IndexOf(control);
            Views.Add(control);
            return Views.IndexOf(control);
        }

        /// <summary>
        /// 注册视图控件到指定位置
        /// </summary>
        public int AddView(int index, Control control)
        {
            if (control == null) return -1;
            if (Views.Contains(control)) return Views.IndexOf(control);
            Views.Insert(Math.Clamp(index, 0, Views.Count), control);
            return Views.IndexOf(control);
        }

        /// <summary>
        /// 移除视图控件
        /// </summary>
        public void RemoveView(Control control)
        {
            Views.Remove(control);
        }

        /// <summary>
        /// 激活指定视图控件的标签页
        /// </summary>
        public void ActiveView(Control control)
        {
            if (!Views.Contains(control))
                AddView(control);
            ActiveViewHandler?.Invoke(control);
            LastActiveView = control;
        }

        /// <summary>
        /// 将所有已注册视图创建为文档标签页
        /// </summary>
        public void ShowAllViews()
        {
            ShowAllViewsHandler?.Invoke();
        }

        /// <summary>
        /// 激活上一次显示的视图
        /// </summary>
        public void ActivateLastView()
        {
            if (LastActiveView != null)
                ActiveViewHandler?.Invoke(LastActiveView);
        }
    }
}
