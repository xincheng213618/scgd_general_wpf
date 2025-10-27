using ColorVision.Common.MVVM;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// 工具栏位置枚举
    /// </summary>
    public enum ToolBarLocal
    {
        Top,
        Draw,
        Left,
        Right,
        RightBottom,
        LeftTop,
        None
    }

    /// <summary>
    /// 编辑器工具接口 - 定义可在工具栏显示的工具
    /// </summary>
    public interface IEditorTool
    {
        ToolBarLocal ToolBarLocal { get; }
        string? GuidId { get; }
        int Order { get; }
        object? Icon { get; }
        ICommand? Command { get; }
    }

    /// <summary>
    /// 可切换编辑器工具接口 - 支持选中/未选中状态
    /// </summary>
    public interface IEditorToggleTool : IEditorTool
    {
        /// <summary>
        /// 可绑定的切换状态。如果状态可以通过编程方式改变，实现应使用 INotifyPropertyChanged
        /// </summary>
        bool IsChecked { get; set; }
    }
    public interface IEditorTextTool : IEditorTool
    {
        public Binding Binding { get; set; }
    }

    /// <summary>
    /// 可切换编辑器工具基类 - 提供默认实现
    /// </summary>
    public abstract class IEditorToggleToolBase : ViewModelBase, IEditorToggleTool
    {
        public virtual ToolBarLocal ToolBarLocal { get; set; } = ToolBarLocal.Draw;
        public virtual string? GuidId => GetType().Name;
        public virtual int Order { get; set; } = 1;
        public virtual object Icon { get; set; }
        public virtual ICommand? Command { get; set; }
        public virtual bool IsChecked { get; set; }
    }

    /// <summary>
    /// 编辑器工具上下文菜单接口
    /// </summary>
    public interface IIEditorToolContextMenu
    {
        List<MenuItemMetadata> GetContextMenuItems();
    }

    /// <summary>
    /// 工具栏位置扩展方法
    /// </summary>
    public static class ToolBarLocalExtensions
    {
        /// <summary>
        /// 根据位置获取对应的工具栏控件
        /// </summary>
        public static ToolBar? GetRegionToolBar(this ImageView imageView, ToolBarLocal loc)
        {
            return loc switch
            {
                ToolBarLocal.Top => imageView.ToolBarTop.ToolBars.Count > 0 ? imageView.ToolBarTop.ToolBars[0] : null,
                ToolBarLocal.Left => imageView.ToolBarLeft.ToolBars.Count > 0 ? imageView.ToolBarLeft.ToolBars[0] : null,
                ToolBarLocal.Right => imageView.ToolBarRight.ToolBars.Count > 0 ? imageView.ToolBarRight.ToolBars[0] : null,
                ToolBarLocal.RightBottom => imageView.ToolBarRight.ToolBars.Count > 0 ? imageView.ToolBarRight.ToolBars[0] : null,
                ToolBarLocal.LeftTop => imageView.ToolBarLeft.ToolBars.Count > 0 ? imageView.ToolBarLeft.ToolBars[0] : null,
                ToolBarLocal.Draw => imageView.ToolBarDraw.ToolBars.Count > 0 ? imageView.ToolBarDraw.ToolBars[0] : null,
                _ => null
            };
        }
    }
}
