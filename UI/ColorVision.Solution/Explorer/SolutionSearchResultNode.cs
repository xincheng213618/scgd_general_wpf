using System.IO;

namespace ColorVision.Solution.Explorer
{
    public sealed class SolutionSearchResultNode : SolutionNode, IDisposable
    {
        private readonly bool _ownsTarget;
        private bool _disposed;

        public SolutionExplorer Explorer { get; }
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
        public override bool CanOpen => TargetNode.CanOpen;
        public override bool CanRefresh => TargetNode.CanRefresh;
        public override bool CanShowProperties => TargetNode.CanShowProperties;
        public override string? EditorResourcePath => TargetNode.EditorResourcePath;
        public override string? ClipboardResourcePath => TargetNode.ClipboardResourcePath;

        internal SolutionSearchResultNode(
            SolutionExplorer explorer,
            SolutionNode targetNode,
            string displayPath,
            bool ownsTarget)
        {
            Explorer = explorer ?? throw new ArgumentNullException(nameof(explorer));
            TargetNode = targetNode ?? throw new ArgumentNullException(nameof(targetNode));
            _displayPath = displayPath ?? string.Empty;
            _ownsTarget = ownsTarget;
            FullPath = targetNode.FullPath;
            Icon = targetNode.Icon;
            CanCopy = targetNode.CanCopy;
            CanCut = targetNode.CanCut;
            CanDelete = targetNode.CanDelete;
            CanReName = targetNode.CanReName;
            Initialize();
        }

        public override bool CanAdd
        {
            get => TargetNode.CanAdd;
            set => TargetNode.CanAdd = value;
        }
        public override bool CanPaste
        {
            get => TargetNode.CanPaste;
            set => TargetNode.CanPaste = value;
        }

        public override void Open()
        {
            TargetNode.Open();
        }

        public override void ShowProperty()
        {
            TargetNode.ShowProperty();
        }

        public override void Refresh()
        {
            TargetNode.Refresh();
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
