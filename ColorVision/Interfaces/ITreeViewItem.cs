using System.Windows.Controls;

namespace ColorVision.Interfaces
{
    public interface ITreeViewItem
    {
        public bool IsExpanded { get; set; }
        public bool IsSelected { get; set; }

        public ContextMenu ContextMenu { get; set; }
    }
}

