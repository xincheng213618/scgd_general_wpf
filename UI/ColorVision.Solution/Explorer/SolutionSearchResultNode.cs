using System.IO;

namespace ColorVision.Solution.Explorer
{
    public sealed class SolutionSearchResultNode : SolutionNode, IDisposable
    {
        private readonly bool _ownsTarget;
        private bool _disposed;

        public SolutionNode TargetNode { get; }

        public string DisplayPath
        {
            get => _displayPath;
            private set
            {
                if (string.Equals(_displayPath, value, StringComparison.Ordinal))
                    return;
                _displayPath = value;
                NotifyPropertyChanged();
            }
        }
        private string _displayPath;

        public override string Name
        {
            get => TargetNode.Name;
            set
            {
                if (string.Equals(TargetNode.Name, value, StringComparison.Ordinal))
                    return;
                string previousDisplayPath = DisplayPath;
                TargetNode.Name = value;
                if (!string.Equals(TargetNode.Name, value, StringComparison.Ordinal))
                    return;

                FullPath = TargetNode.FullPath;
                DisplayPath = ReplaceDisplayName(previousDisplayPath, TargetNode.Name);
                NotifyPropertyChanged();
            }
        }

        public override bool IsEditMode
        {
            get => TargetNode.IsEditMode;
            set
            {
                if (TargetNode.IsEditMode == value)
                    return;
                TargetNode.IsEditMode = value;
                NotifyPropertyChanged();
            }
        }

        public override bool IsStartupProject => TargetNode.IsStartupProject;

        internal SolutionSearchResultNode(
            SolutionNode targetNode,
            string displayPath,
            bool ownsTarget)
        {
            TargetNode = targetNode ?? throw new ArgumentNullException(nameof(targetNode));
            _displayPath = displayPath ?? string.Empty;
            _ownsTarget = ownsTarget;
            FullPath = targetNode.FullPath;
            Icon = targetNode.Icon;
            CanAdd = targetNode.CanAdd;
            CanCopy = targetNode.CanCopy;
            CanCut = targetNode.CanCut;
            CanDelete = targetNode.CanDelete;
            CanPaste = targetNode.CanPaste;
            CanReName = targetNode.CanReName;
            Initialize();
        }

        public override void Open()
        {
            TargetNode.Open();
        }

        public override void ShowProperty()
        {
            TargetNode.ShowProperty();
        }

        public override void CopyFullPath()
        {
            TargetNode.CopyFullPath();
        }

        public override void Copy()
        {
            TargetNode.Copy();
        }

        internal override bool TryDelete(bool showConfirmation)
        {
            return TargetNode.TryDelete(showConfirmation);
        }

        public override void InitMenuItem()
        {
            MenuItemMetadatas.Clear();
            TargetNode.CollectMenuItems(MenuItemMetadatas);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            TargetNode.IsEditMode = false;
            if (_ownsTarget && TargetNode is IDisposable disposable)
                disposable.Dispose();
            VisualChildren.Clear();
        }

        private static string ReplaceDisplayName(string displayPath, string newName)
        {
            if (string.IsNullOrWhiteSpace(displayPath))
                return newName;
            int separatorIndex = displayPath.LastIndexOfAny(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
            return separatorIndex < 0
                ? newName
                : displayPath[..(separatorIndex + 1)] + newName;
        }
    }
}
