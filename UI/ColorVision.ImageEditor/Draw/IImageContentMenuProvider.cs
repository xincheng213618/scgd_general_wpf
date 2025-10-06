using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// ��ͼԪ�������Ĳ˵��ӿ�
    /// </summary>
    public interface IDVContextMenu
    {
        Type ContextType { get; }
        IEnumerable<MenuItem> GetContextMenuItems(ImageViewModel imageViewModel, object obj);
    }


    /// <summary>
    /// ��ͼ�༭��������
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
