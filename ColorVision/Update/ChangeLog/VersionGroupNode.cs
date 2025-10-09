using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ColorVision.Update
{
    /// <summary>
    /// Base class for version tree nodes
    /// </summary>
    public abstract class VersionTreeNode : ViewModelBase
    {
        [DisplayName("Version")]
        public string DisplayName { get; set; }

        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                NotifyPropertyChanged();
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                NotifyPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Major version group node (e.g., "1.x.x.x")
    /// </summary>
    public class MajorVersionNode : VersionTreeNode
    {
        public int MajorVersion { get; set; }
        public ObservableCollection<MinorVersionNode> MinorVersions { get; set; } = new ObservableCollection<MinorVersionNode>();
    }

    /// <summary>
    /// Minor version group node (e.g., "1.3.x.x")
    /// </summary>
    public class MinorVersionNode : VersionTreeNode
    {
        public int MajorVersion { get; set; }
        public int MinorVersion { get; set; }
        public ObservableCollection<ChangeLogEntry> Entries { get; set; } = new ObservableCollection<ChangeLogEntry>();
    }
}
