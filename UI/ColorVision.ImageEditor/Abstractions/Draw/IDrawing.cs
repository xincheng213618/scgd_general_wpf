using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{

    /// <summary>
    /// 绘图数据可视化接口 - 用于数据驱动的绘图元素
    /// </summary>
    public interface IDrawingVisualDatum
    {
        BaseProperties BaseAttribute { get; }
        Pen Pen { get; set; }
        void Render();
    }

    /// <summary>
    /// 选择可视化接口 - 定义可选择元素的矩形区域操作
    /// </summary>
    public interface ISelectVisual
    {
        Rect GetRect();
        void SetRect(Rect rect);
    }

    /// <summary>
    /// 文本属性接口 - 定义带文本的绘图元素
    /// </summary>
    public interface ITextProperties
    {
        TextAttribute TextAttribute { get; set; }
        string Text { get; set; }
        bool IsShowText { get; set; }
    }
}

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// 绘图元素上下文菜单接口
    /// </summary>
    public interface IDVContextMenu
    {
        Type ContextType { get; }
        IEnumerable<MenuItem> GetContextMenuItems(EditorContext context, object obj);
    }

    /// <summary>
    /// 绘图可视化上下文菜单实现
    /// </summary>
    public class IDrawingVisualDVContextMenu : IDVContextMenu
    {
        public Type ContextType => typeof(IDrawingVisual);

        public IEnumerable<MenuItem> GetContextMenuItems(EditorContext context, object obj)
        {
            List<MenuItem> MenuItems = new List<MenuItem>();
            if (obj is IDrawingVisual drawingVisual)
            {
                // 可以在此添加通用菜单项
            }
            return MenuItems;
        }
    }

    /// <summary>
    /// 绘图编辑器管理器 - 管理当前活动的绘图工具
    /// </summary>
    public class DrawEditorManager
    {
        public IEditorToggleTool? Current { get; set; }

        public void SetCurrentDrawEditor(IEditorToggleTool? drawEditor)
        {
            if (Current != null)
            {
                Current.IsChecked = false;
            }
            Current = drawEditor;
            if (Current != null)
            {
                Current.IsChecked = true;
            }
        }
    }
}
