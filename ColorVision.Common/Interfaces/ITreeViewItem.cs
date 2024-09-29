using System.Windows.Controls;

namespace ColorVision.UI
{
    public interface IContextMenu
    {
        public ContextMenu ContextMenu { get; set; }
    }

    public interface ITreeViewItem
    {
        public bool IsExpanded { get; set; }
        public bool IsSelected { get; set; }

        public ContextMenu ContextMenu { get; set; }
    }

    public interface IEditable
    {
        bool IsEditMode { get; set; }
    }
}

