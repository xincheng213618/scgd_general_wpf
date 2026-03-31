using ColorVision.UI.StatusBar;
using System.Collections.Generic;
using System.Windows.Controls;

namespace ColorVision.UI
{
    /// <summary>
    /// 状态栏管理器，管理所有注册的 StatusBarControl 实例。
    /// 现在作为轻量级注册中心，核心逻辑已移至 StatusBarControl。
    /// </summary>
    public class StatusBarManager
    {
        private static StatusBarManager _instance;
        private static readonly object _locker = new();
        public static StatusBarManager GetInstance() { lock (_locker) { return _instance ??= new StatusBarManager(); } }

        private readonly Dictionary<string, StatusBarControl> _controls = new();

        private StatusBarManager() { }

        /// <summary>
        /// 注册一个 StatusBarControl 实例
        /// </summary>
        public void Register(string targetName, StatusBarControl control)
        {
            _controls[targetName] = control;
        }

        /// <summary>
        /// 获取指定窗口的 StatusBarControl
        /// </summary>
        public StatusBarControl GetControl(string targetName)
        {
            _controls.TryGetValue(targetName, out var control);
            return control;
        }

        /// <summary>
        /// 兼容旧接口：使用 MainWindowTarget 初始化
        /// </summary>
        public void Init(string targetName, Grid container)
        {
            var control = new StatusBarControl { TargetName = targetName };

            // 将 StatusBarControl 放入 Grid 容器
            container.Children.Clear();
            container.Children.Add(control);
            Grid.SetColumnSpan(control, 3);

            Register(targetName, control);
        }

        /// <summary>
        /// 刷新所有已注册的状态栏
        /// </summary>
        public void RefreshAll()
        {
            foreach (var control in _controls.Values)
                control.LoadItems();
        }

        /// <summary>
        /// 为指定窗口设置动态状态栏项
        /// </summary>
        public void SetDynamicItems(string targetName, IEnumerable<StatusBarMeta> items)
        {
            if (_controls.TryGetValue(targetName, out var control))
                control.SetDynamicItems(items);
        }

        /// <summary>
        /// 清除指定窗口的动态状态栏项
        /// </summary>
        public void ClearDynamicItems(string targetName)
        {
            if (_controls.TryGetValue(targetName, out var control))
                control.ClearDynamicItems();
        }
    }
}