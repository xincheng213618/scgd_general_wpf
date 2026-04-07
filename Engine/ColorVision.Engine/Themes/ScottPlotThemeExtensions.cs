using System;
using System.Windows;

namespace ColorVision.Themes
{
    /// <summary>
    /// ScottPlot 主题扩展方法
    /// 提供简洁的 API 让 WpfPlot 控件支持自动主题切换
    /// </summary>
    public static class ScottPlotThemeExtensions
    {
        /// <summary>
        /// 注册图表以自动跟随 ColorVision 主题变化
        /// 使用弱引用事件机制，不会阻止图表控件被 GC 回收
        /// </summary>
        /// <param name="plot">ScottPlot.WPF.WpfPlot 控件</param>
        /// <param name="autoRefresh">主题变更时是否自动刷新（默认true）</param>
        /// <example>
        /// // 在 UserControl_Initialized 或构造函数中调用：
        /// wpfplot1.EnableThemeSupport();
        /// wpfplot2.EnableThemeSupport();
        /// </example>
        public static void EnableThemeSupport(this object plot, bool autoRefresh = true)
        {
            ScottPlotThemeManager.RegisterPlot(plot, autoRefresh);
        }

        /// <summary>
        /// 取消注册图表的自动主题跟随
        /// 在控件卸载时调用，避免内存泄漏
        /// </summary>
        /// <param name="plot">ScottPlot.WPF.WpfPlot 控件</param>
        /// <example>
        /// // 在 Unloaded 事件中调用：
        /// wpfplot1.DisableThemeSupport();
        /// </example>
        public static void DisableThemeSupport(this object plot)
        {
            ScottPlotThemeManager.UnregisterPlot(plot);
        }

        /// <summary>
        /// 立即应用当前主题到图表（一次性，不自动跟随）
        /// </summary>
        /// <param name="plot">ScottPlot.WPF.WpfPlot 控件</param>
        /// <param name="refresh">是否立即刷新</param>
        public static void ApplyCurrentTheme(this object plot, bool refresh = true)
        {
            ScottPlotThemeManager.ApplyTheme(plot, refresh);
        }

        /// <summary>
        /// 为 WpfPlot 控件附加主题支持（附加属性方式）
        /// 可在 XAML 中直接使用
        /// </summary>
        public static readonly DependencyProperty EnableThemeProperty =
            DependencyProperty.RegisterAttached(
                "EnableTheme",
                typeof(bool),
                typeof(ScottPlotThemeExtensions),
                new PropertyMetadata(false, OnEnableThemeChanged));

        /// <summary>
        /// 获取 EnableTheme 附加属性值
        /// </summary>
        public static bool GetEnableTheme(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableThemeProperty);
        }

        /// <summary>
        /// 设置 EnableTheme 附加属性值
        /// </summary>
        public static void SetEnableTheme(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableThemeProperty, value);
        }

        /// <summary>
        /// 附加属性变更处理
        /// </summary>
        private static void OnEnableThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement element) return;

            // 检查是否是 WpfPlot 类型（通过类型名称判断，避免直接引用 ScottPlot.WPF）
            if (!IsWpfPlotType(element)) return;

            if ((bool)e.NewValue)
            {
                // 启用主题支持
                ScottPlotThemeManager.RegisterPlot(element, true);

                // 监听 Unloaded 事件以自动清理
                element.Unloaded += OnPlotUnloaded;
            }
            else
            {
                // 禁用主题支持
                ScottPlotThemeManager.UnregisterPlot(element);
                element.Unloaded -= OnPlotUnloaded;
            }
        }

        /// <summary>
        /// 控件卸载时自动取消注册
        /// </summary>
        private static void OnPlotUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                ScottPlotThemeManager.UnregisterPlot(element);
                element.Unloaded -= OnPlotUnloaded;
            }
        }

        /// <summary>
        /// 判断对象是否是 WpfPlot 类型
        /// </summary>
        private static bool IsWpfPlotType(object obj)
        {
            var typeName = obj.GetType().FullName;
            return typeName?.Contains("ScottPlot") == true && typeName?.Contains("WpfPlot") == true;
        }
    }
}
