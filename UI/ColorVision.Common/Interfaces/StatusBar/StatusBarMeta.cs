using System;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI
{
    public class StatusBarMeta
    {
        /// <summary>
        /// 唯一标识，用于持久化可见性偏好
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 显示名称，也用于右键菜单
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 描述/ToolTip 文本
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 内容类型: Icon / Text / IconText
        /// </summary>
        public StatusBarType Type { get; set; } = StatusBarType.Icon;

        /// <summary>
        /// 对齐方向: Left / Right
        /// </summary>
        public StatusBarAlignment Alignment { get; set; } = StatusBarAlignment.Right;

        /// <summary>
        /// 点击行为: Command(执行命令) / Popup(弹出面板)
        /// </summary>
        public StatusBarActionType ActionType { get; set; } = StatusBarActionType.Command;

        /// <summary>
        /// 排序顺序，同侧内从小到大排列
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// 绑定属性名(Icon用于IsChecked, Text用于Content)
        /// </summary>
        public string BindingName { get; set; }

        /// <summary>
        /// 数据源对象
        /// </summary>
        public object Source { get; set; }

        /// <summary>
        /// 左键点击命令
        /// </summary>
        public ICommand Command { get; set; }

        /// <summary>
        /// 目标窗口名称，用来区分不同窗口的状态栏项
        /// 默认 "Global" 表示所有窗口都显示
        /// </summary>
        public string TargetName { get; set; } = "Global";

        /// <summary>
        /// 图标资源键(DrawingBrush)
        /// </summary>
        public string IconResourceKey { get; set; }

        /// <summary>
        /// 图标内容(直接提供UI元素或ImageSource)
        /// </summary>
        public object IconContent { get; set; }

        /// <summary>
        /// IconText模式下文字绑定属性名
        /// </summary>
        public string TextBindingName { get; set; }

        /// <summary>
        /// Popup模式下的内容工厂方法
        /// </summary>
        public Func<FrameworkElement> PopupContentFactory { get; set; }

        /// <summary>
        /// 默认是否可见（用户可通过右键菜单切换）
        /// </summary>
        public bool IsVisible { get; set; } = true;
    }
}
