using System.Windows.Controls;

namespace ColorVision.Services.Core
{
    public interface ITreeViewItem
    {
        public bool IsExpanded { get; set; }
        public bool IsSelected { get; set; }

        public ContextMenu ContextMenu { get; set; }
    }
}

