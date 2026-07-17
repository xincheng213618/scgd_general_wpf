using ColorVision.Common.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Media;
using System.Runtime.Serialization;
using System.IO;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.Solution.Explorer
{
    public interface ISolutionNode
    {
        public SolutionNode Parent { get; set; }
        public ObservableCollection<SolutionNode> VisualChildren { get; set; }
        public void AddChild(SolutionNode node);
        public void RemoveChild(SolutionNode node);
    }

    public enum SolutionDeleteKind
    {
        DeletePhysicalResource,
        RemoveFromSolution,
        RemoveSolutionFolder,
    }

    [DataContract]
    public class SolutionNode : INotifyPropertyChanged, ISolutionNode
    {
        public SolutionNode Parent { get; set; }

        public virtual ObservableCollection<SolutionNode> VisualChildren
        {
            get => _visualChildren;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                if (ReferenceEquals(_visualChildren, value))
                    return;
                _visualChildren = value;
                NotifyPropertyChanged();
            }
        }
        private ObservableCollection<SolutionNode> _visualChildren = new();

        public event EventHandler AddChildEventHandler;

        public virtual void AddChild(SolutionNode node)
        {
            if (node == null) return;
            if (!string.IsNullOrEmpty(node.FullPath) &&
                VisualChildren.Any(child => string.Equals(child.FullPath, node.FullPath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            node.Parent = this;
            node.UpdateProjectMembershipState();
            AddChildEventHandler?.Invoke(this, new EventArgs());
            VisualChildren.SortedAdd(node);
        }

        internal void ReplaceLazyChildren(
            IEnumerable<SolutionNode> loadedChildren,
            bool loadedChildrenAreSorted = false)
        {
            ArgumentNullException.ThrowIfNull(loadedChildren);

            var children = VisualChildren
                .Where(child => child is not LazyLoadingNode)
                .ToList();
            bool hasExistingChildren = children.Count > 0;
            var existingPaths = children
                .Where(child => !string.IsNullOrWhiteSpace(child.FullPath))
                .Select(child => child.FullPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (SolutionNode child in loadedChildren)
            {
                if (!string.IsNullOrWhiteSpace(child.FullPath)
                    && !existingPaths.Add(child.FullPath))
                {
                    if (child is IDisposable disposable)
                        disposable.Dispose();
                    continue;
                }
                children.Add(child);
            }

            if (hasExistingChildren || !loadedChildrenAreSorted)
            {
                children.Sort((left, right) =>
                {
                    int result = left.CompareTo(right);
                    return result != 0
                        ? result
                        : StringComparer.OrdinalIgnoreCase.Compare(left.FullPath, right.FullPath);
                });
            }

            foreach (SolutionNode child in children)
            {
                child.Parent = this;
                child.UpdateProjectMembershipState();
            }
            VisualChildren = new ObservableCollection<SolutionNode>(children);
            AddChildEventHandler?.Invoke(this, EventArgs.Empty);
        }
        public event EventHandler RemoveChildEventHandler;
        public virtual void RemoveChild(SolutionNode node)
        {
            this.VisualChildren.Remove(node);
            RemoveChildEventHandler?.Invoke(this, new EventArgs());
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public virtual string Name { get => Name1; set
            { 
                if (Name1 == value) return; 
                if (!IsEditMode || ReName(value))
                {
                    Name1 = value;
                }
                NotifyPropertyChanged();  
            } 
        }
        protected string Name1 { get; set; } = string.Empty;

        public virtual string FullPath { get => _FullPath; set { _FullPath = value; NotifyPropertyChanged(); } }
        private string _FullPath = string.Empty;

        public virtual bool IsEditMode
        {
            get  => _IsEditMode;
            set {_IsEditMode = value; NotifyPropertyChanged(); }
        }
        private bool _IsEditMode ;

        public virtual ImageSource? Icon { get; set; }

        public virtual bool IsExpanded { get => _IsExpanded; set { _IsExpanded = value; NotifyPropertyChanged(); } }
        private bool _IsExpanded;

        public virtual bool IsSelected { get => _IsSelected; set { _IsSelected = value; NotifyPropertyChanged(); } }
        private bool _IsSelected;

        /// <summary>
        /// Separate from IsSelected (which TreeView controls for single-select).
        /// IsMultiSelected is managed entirely by TreeViewControl for multi-select visual feedback.
        /// </summary>
        public bool IsMultiSelected { get => _IsMultiSelected; set { _IsMultiSelected = value; NotifyPropertyChanged(); } }
        private bool _IsMultiSelected;

        /// <summary>
        /// Indicates that this node is the currently validated drag-and-drop target.
        /// It is independent from selection so drag feedback never changes command state.
        /// </summary>
        public bool IsDropTarget { get => _IsDropTarget; set { _IsDropTarget = value; NotifyPropertyChanged(); } }
        private bool _IsDropTarget;

        public virtual bool IsStartupProject => false;

        /// <summary>Whether the node supports its primary open action.</summary>
        public virtual bool CanOpen => false;
        public virtual bool CanRefresh => false;
        public virtual bool CanShowProperties => false;

        /// <summary>
        /// Physical file or folder offered to explicit editors. This can differ
        /// from FullPath, which may represent a tree identity or project root.
        /// </summary>
        public virtual string? EditorResourcePath => null;
        /// <summary>
        /// Existing physical file or directory placed on the Windows clipboard.
        /// FullPath remains a tree identity and must not be used as a substitute.
        /// </summary>
        public virtual string? ClipboardResourcePath => null;
        public bool IsExcludedFromProject { get; private set; }

        public virtual void ShowProperty() { }

        public virtual void Refresh() { }

        public virtual void Delete()
        {
            TryDelete(showConfirmation: true);
        }

        internal virtual bool TryDelete(bool showConfirmation)
        {
            Parent?.RemoveChild(this);
            return true;
        }

        internal virtual string? PhysicalDeletePath => null;

        internal virtual bool CompletePhysicalDelete()
        {
            Parent?.RemoveChild(this);
            if (this is IDisposable disposable)
                disposable.Dispose();
            return true;
        }

        public virtual bool CanReName { get; set; }
        public virtual bool CanDelete { get; set; } = true;
        public virtual SolutionDeleteKind DeleteKind => SolutionDeleteKind.DeletePhysicalResource;
        public virtual bool CanAdd
        {
            get => _canAddEnabled
                && this is ISolutionContainerNode container
                && container.SupportedContainerActions != SolutionContainerAction.None;
            set
            {
                if (_canAddEnabled == value)
                    return;
                _canAddEnabled = value;
                NotifyPropertyChanged();
            }
        }
        private bool _canAddEnabled = true;
        public virtual bool CanCopy { get; set; }
        public virtual bool CanPaste
        {
            get => _canPasteEnabled
                && this is ISolutionPhysicalContainer container
                && Directory.Exists(container.PhysicalContainerPath);
            set
            {
                if (_canPasteEnabled == value)
                    return;
                _canPasteEnabled = value;
                NotifyPropertyChanged();
            }
        }
        private bool _canPasteEnabled = true;
        public virtual bool CanCut { get; set; }

        public virtual void Open() { }

        public virtual void CopyFullPath()
        {
            if (!string.IsNullOrEmpty(FullPath))
            {
                Common.Clipboard.SetText(FullPath);
            }
        }

        internal void UpdateProjectMembershipState()
        {
            bool isExcluded = this is not ProjectNode
                && ProjectNode.FindOwningProject(this) is { } projectNode
                && !projectNode.IsPathIncludedByProjectRules(FullPath);
            if (IsExcludedFromProject == isExcluded)
                return;

            IsExcludedFromProject = isExcluded;
            NotifyPropertyChanged(nameof(IsExcludedFromProject));
        }

        protected virtual void LogOperation(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[SolutionNode] {DateTime.Now:yyyy-MM-dd HH:mm:ss} INFO: {message}");
        }

        protected virtual void LogError(string message, Exception? exception = null)
        {
            var fullMessage = exception != null ? $"{message}\nException: {exception}" : message;
            System.Diagnostics.Debug.WriteLine($"[SolutionNode] {DateTime.Now:yyyy-MM-dd HH:mm:ss} ERROR: {fullMessage}");
        }

        protected virtual void ShowUserError(string message)
        {
            MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        protected virtual void ShowUserInfo(string message)
        {
            MessageBox.Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public virtual bool ReName(string name) => false;

        public virtual int CompareTo(object obj)
        {
            if (obj == null) return -1;
            else if (obj == this) return 0;
            else if (obj is SolutionNode node) return Common.NativeMethods.Shlwapi.CompareLogical(Name, node.Name);
            else return -1;
        }
    }

    public sealed class LazyLoadingNode : SolutionNode
    {
        public LazyLoadingNode()
        {
            Name1 = "Loading...";
            CanCopy = false;
            CanCut = false;
            CanDelete = false;
            CanReName = false;
        }
    }
}
