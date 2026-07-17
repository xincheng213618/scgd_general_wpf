using ColorVision.Common.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ColorVision.Common.MVVM;
using System.Runtime.Serialization;
using ColorVision.UI;
using ColorVision.UI.Menus;
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

        public RelayCommand AddChildrenCommand { get; set; }
        public RelayCommand RemoveChildrenCommand { get; set; }
        public RelayCommand OpenCommand { get; set; }
        public RelayCommand DeleteCommand { get; set; }
        public RelayCommand CopyFullPathCommand { get; set; }

        public RelayCommand PropertyCommand { get; set; }

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

        /// <summary>
        /// Physical file or folder offered to explicit editors. This can differ
        /// from FullPath, which may represent a tree identity or project root.
        /// </summary>
        public virtual string? EditorResourcePath => null;
        public bool IsExcludedFromProject { get; private set; }

        public List<MenuItemMetadata> MenuItemMetadatas { get; set; }

        private bool _menuInitialized;

        public SolutionNode()
        {
            MenuItemMetadatas = new List<MenuItemMetadata>();
        }

        public virtual void Initialize()
        {
            OpenCommand = new RelayCommand(_ => Open(), _ => CanOpen);
            DeleteCommand = new RelayCommand(s => Delete());
            PropertyCommand = new RelayCommand(s => ShowProperty());
            CopyFullPathCommand = new RelayCommand(s => CopyFullPath(), s => !string.IsNullOrEmpty(FullPath));
        }

        public virtual void InitMenuItem()
        {
            MenuItemMetadatas.Clear();
            AddProjectMembershipMenuItem();
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = SolutionCommandIds.Cut, Order = 100, Command = ApplicationCommands.Cut, Header = UI.Properties.Resources.MenuCut ,Icon = MenuItemIcon.TryFindResource("DICut") ,InputGestureText = "Ctrl+X" });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = SolutionCommandIds.Copy, Order = 101, Command = ApplicationCommands.Copy, Header = UI.Properties.Resources.MenuCopy, Icon = MenuItemIcon.TryFindResource("DICopy"), InputGestureText = "Ctrl+C" });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = SolutionCommandIds.Paste, Order = 102, Command = ApplicationCommands.Paste, Header = UI.Properties.Resources.MenuPaste, Icon =MenuItemIcon.TryFindResource("DIPaste"), InputGestureText = "Ctrl+V" });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = SolutionCommandIds.Delete, Order = 103, Command = ApplicationCommands.Delete, Header = UI.Properties.Resources.MenuDelete,Icon = MenuItemIcon.TryFindResource("DIDelete"), InputGestureText = "Del" });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = SolutionCommandIds.Rename, Order = 104, Command = Commands.ReName, Header = UI.Properties.Resources.MenuRename ,Icon = MenuItemIcon.TryFindResource("DIRename"), InputGestureText = "F2" });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Property", Order = 9999, Command = PropertyCommand, Header = ColorVision.Solution.Properties.Resources.MenuProperty, Icon = MenuItemIcon.TryFindResource("DIProperty") });
        }

        private void AddProjectMembershipMenuItem()
        {
            if (this is ProjectNode
                || string.IsNullOrWhiteSpace(FullPath)
                || ProjectNode.FindOwningProject(this) is not { } projectNode
                || !ProjectProviderRegistry.CanChangeProjectItemMembership(projectNode.Project, FullPath))
            {
                return;
            }

            bool isIncluded = projectNode.IsPathIncludedByProjectRules(FullPath);
            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                GuidId = isIncluded ? "ExcludeFromProject" : "IncludeInProject",
                Order = 90,
                Header = isIncluded ? "从项目中排除(_J)" : "包括在项目中(_J)",
                Command = isIncluded
                    ? SolutionProjectCommands.ExcludeFromProject
                    : SolutionProjectCommands.IncludeInProject,
            });
        }

        /// <summary>
        /// Collect menu items for the shared context menu service.
        /// Called on-demand when the shared ContextMenu opens on this node.
        /// This avoids allocating per-node menus until needed.
        /// </summary>
        public void CollectMenuItems(List<MenuItemMetadata> target)
        {
            if (!_menuInitialized)
            {
                InitMenuItem();
                _menuInitialized = true;
            }
            target.AddRange(MenuItemMetadatas);
        }

        internal void InvalidateMenuItems()
        {
            _menuInitialized = false;
            MenuItemMetadatas.Clear();
        }

        public virtual void ShowProperty() { }

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

        public virtual bool CanReName { get; set; } = true;
        public virtual bool CanDelete { get; set; } = true;
        public virtual bool CanAdd { get; set; } = true;
        public virtual bool CanCopy { get; set; } = true;
        public virtual bool CanPaste { get; set; } = true;
        public virtual bool CanCut { get; set; } = true;

        public void MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Open();
        }

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

        public virtual void Copy()
        {
            throw new NotImplementedException();
        }

        public virtual bool ReName(string name)
        {
            throw new NotImplementedException();
        }

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
            CanAdd = false;
            CanCopy = false;
            CanCut = false;
            CanDelete = false;
            CanPaste = false;
            CanReName = false;
        }
    }
}
