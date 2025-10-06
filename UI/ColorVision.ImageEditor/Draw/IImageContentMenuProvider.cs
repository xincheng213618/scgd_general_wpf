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
    /// 绘图编辑器管理类
    /// </summary>
    public class DrawEditorManager
    {
        public IEditorToggleTool? Current { get; set; }

        public void SetCurrentDrawEditor(IEditorToggleTool? drawEditor)
        {
            if(Current != null)
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
