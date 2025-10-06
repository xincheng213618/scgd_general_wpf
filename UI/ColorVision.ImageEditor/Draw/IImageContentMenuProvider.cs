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
    /// ��ͼ�༭���ӿ�
    /// </summary>
    public interface IDrawEditor
    {
        public bool IsShow { get; set; }
    }

    /// <summary>
    /// ��ͼ�༭��������
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
