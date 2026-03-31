using System.Windows.Input;

namespace ColorVision.UI
{
    public class StatusBarMeta
    {
        public string Name { get; set; }

        /// <summary>
        /// 描述项，用作ToolTip显示
        /// </summary>
        public string Description { get; set; }

        public StatusBarType Type { get; set; } = StatusBarType.Icon;

        /// <summary>
        /// 如果需要变更顺序，可以通过Order来控制
        /// </summary>
        public int Order { get; set; }

        public string BindingName { get; set; }

        public string VisibilityBindingName { get; set; }

        public string ButtonStyleName { get; set; }

        public object Source { get; set; }

        /// <summary>
        /// 左键点击命令（执行指令或弹出对话框）
        /// </summary>
        public ICommand Command { get; set; }

        /// <summary>
        /// 状态栏项的对齐方式（左侧或右侧），为null时根据Type自动推断：Icon/IconText→Left, Text→Right
        /// </summary>
        public StatusBarAlignment? Alignment { get; set; }

        /// <summary>
        /// 目标窗口名称，用于区分不同窗口的状态栏项，默认为Global（所有窗口都加载）
        /// </summary>
        public string TargetName { get; set; } = StatusBarConstants.GlobalTarget;

        /// <summary>
        /// 右键点击命令（用于弹出菜单或对话框）
        /// </summary>
        public ICommand RightClickCommand { get; set; }

        /// <summary>
        /// 图标资源键名，用于IconText类型中显示图标
        /// </summary>
        public string IconResourceKey { get; set; }

        /// <summary>
        /// 文本绑定属性名，用于IconText类型中显示文本
        /// </summary>
        public string TextBindingName { get; set; }
    }
}
