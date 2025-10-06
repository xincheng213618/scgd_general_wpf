using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// 绘图元素上下文菜单接口
    /// </summary>
    public interface IDVContextMenu
    {
        Type ContextType { get; }
        IEnumerable<MenuItem> GetContextMenuItems(ImageViewModel imageViewModel, object obj);
    }

    /// <summary>
    /// 绘图编辑器接口
    /// </summary>
    public interface IDrawEditor
    {
        public bool IsShow { get; set; }
    }

    /// <summary>
    /// 绘图编辑器管理类
    /// </summary>
    public class DrawEditorManager
    {
        public IDrawEditor? Current { get; set; }

        public void SetCurrentDrawEditor(IDrawEditor? drawEditor)
        {
            if(Current != null)
            {
                Current.IsShow = false;
            }
            Current = drawEditor;
            if (Current != null)
            {
                Current.IsShow = true;
            }
        }
    }
}
