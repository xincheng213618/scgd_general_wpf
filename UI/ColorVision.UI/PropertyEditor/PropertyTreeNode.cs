using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI
{
    /// <summary>
    /// Tree node for PropertyEditorWindow TreeView binding
    /// </summary>
    public class PropertyTreeNode : ViewModelBase
    {
        private string _header = string.Empty;
        public string Header
        {
            get => _header;
            set => SetProperty(ref _header, value);
        }

        private StackPanel? _associatedBorder;
        public StackPanel? AssociatedBorder
        {
            get => _associatedBorder;
            set => SetProperty(ref _associatedBorder, value);
        }

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (SetProperty(ref _isVisible, value))
                {
                    OnPropertyChanged(nameof(Visibility));
                }
            }
        }

        public Visibility Visibility => IsVisible ? Visibility.Visible : Visibility.Collapsed;

        public ObservableCollection<PropertyTreeNode> Children { get; } = new ObservableCollection<PropertyTreeNode>();

        public PropertyTreeNode()
        {
        }

        public PropertyTreeNode(string header, StackPanel? associatedPanel = null)
        {
            Header = header;
            AssociatedBorder = associatedPanel;
        }

        /// <summary>
        /// Recursively show all nodes
        /// </summary>
        public void ShowAll()
        {
            IsVisible = true;
            foreach (var child in Children)
            {
                child.ShowAll();
            }
        }

        /// <summary>
        /// Update visibility based on associated border visibility
        /// </summary>
        public void SyncVisibilityFromBorder()
        {
            if (AssociatedBorder != null)
            {
                IsVisible = AssociatedBorder.Visibility == Visibility.Visible;
            }
            foreach (var child in Children)
            {
                child.SyncVisibilityFromBorder();
            }
        }
    }
}
