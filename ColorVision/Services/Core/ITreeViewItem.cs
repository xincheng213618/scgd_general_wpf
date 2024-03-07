#pragma warning disable CS8625
using System.Windows.Controls;

namespace ColorVision.Services.Core
{
    public interface ITreeViewItem
    {
        public bool IsExpanded { get; set; }
        public bool IsChecked { get; set; }

        public ContextMenu ContextMenu { get; set; }

    }
}
