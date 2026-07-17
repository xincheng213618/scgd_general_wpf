using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.IO;
using System.Windows;

namespace ColorVision.Solution.Explorer
{
    /// <summary>
    /// A file referenced by the solution but not owned by a project. Its tree
    /// placement is virtual: removing the node only updates the solution file.
    /// </summary>
    public sealed class SolutionItemNode : FileNode, IDisposable
    {
        internal override string? PhysicalDeletePath => null;

        private readonly SolutionExplorer _solutionExplorer;
        private FileSystemWatcher? _externalWatcher;
        private bool _disposed;

        public SolutionItemDefinition Definition { get; private set; }
        public string ItemId => Definition.Id;
        internal SolutionExplorer SolutionExplorer => _solutionExplorer;

        public SolutionItemNode(
            SolutionExplorer solutionExplorer,
            SolutionItemDefinition definition,
            string fullPath)
            : base(SolutionNodeFactory.CreateFileMeta(new FileInfo(fullPath)))
        {
            _solutionExplorer = solutionExplorer;
            Definition = definition;
            CanCut = false;
            CanPaste = false;
            CanReName = false;
            UpdateState(definition, fullPath);
            InitializeExternalWatcher();
        }

        public override void InitMenuItem()
        {
            base.InitMenuItem();
            MenuItemMetadatas.RemoveAll(item => item.GuidId is
                SolutionCommandIds.Cut or SolutionCommandIds.Paste or SolutionCommandIds.Rename);
            if (MenuItemMetadatas.FirstOrDefault(item => item.GuidId == SolutionCommandIds.Delete) is { } removeItem)
                removeItem.Header = "从解决方案中移除(_V)";

            IReadOnlyList<(string? Id, string DisplayName)> moveOptions =
                _solutionExplorer.GetSolutionFolderOptions();
            if (moveOptions.Count > 1)
            {
                const string moveMenuId = "MoveSolutionItem";
                MenuItemMetadatas.Add(new MenuItemMetadata
                {
                    GuidId = moveMenuId,
                    Order = 80,
                    Header = "移动到解决方案文件夹(_M)",
                });
                string? currentFolderId = _solutionExplorer.GetSolutionItemFolderId(ItemId);
                int order = 0;
                foreach (var option in moveOptions)
                {
                    string? targetFolderId = option.Id;
                    MenuItemMetadatas.Add(new MenuItemMetadata
                    {
                        OwnerGuid = moveMenuId,
                        GuidId = $"MoveSolutionItem.{targetFolderId ?? "Root"}",
                        Order = order++,
                        Header = option.DisplayName,
                        IsChecked = string.Equals(
                            currentFolderId,
                            targetFolderId,
                            StringComparison.OrdinalIgnoreCase),
                        Command = new RelayCommand(_ =>
                            _solutionExplorer.MoveSolutionItemToFolder(ItemId, targetFolderId)),
                    });
                }
            }
        }

        internal bool CanReuseFor(SolutionItemDefinition definition, string fullPath)
        {
            return string.Equals(ItemId, definition.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(FullPath, fullPath, StringComparison.OrdinalIgnoreCase);
        }

        internal void UpdateState(SolutionItemDefinition definition, string fullPath)
        {
            Definition = definition;
            FileInfo = new FileInfo(fullPath);
            FullPath = FileInfo.FullName;
            Name1 = FileInfo.Exists ? FileInfo.Name : $"{FileInfo.Name} (缺失)";
            CanCopy = FileInfo.Exists;
            NotifyPropertyChanged(nameof(Name));
            NotifyPropertyChanged(nameof(FileInfo));
            InvalidateMenuItems();
        }

        internal override bool TryDelete(bool showConfirmation)
        {
            if (showConfirmation
                && MessageBox.Show(
                    Application.Current?.GetActiveWindow(),
                    $"从解决方案中移除“{Name}”吗？磁盘文件不会被删除。",
                    "ColorVision",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question) != MessageBoxResult.OK)
            {
                return false;
            }

            return _solutionExplorer.RemoveSolutionItem(ItemId);
        }

        private void InitializeExternalWatcher()
        {
            if (_solutionExplorer.IsPathWithinSolution(FullPath)
                || FileInfo.Directory is not { Exists: true } directory)
            {
                return;
            }

            _externalWatcher = new FileSystemWatcher(directory.FullName, FileInfo.Name)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            FileSystemEventHandler refreshHandler = (_, _) => _solutionExplorer.RefreshExplicitProjectState();
            RenamedEventHandler renameHandler = (_, _) => _solutionExplorer.RefreshExplicitProjectState();
            _externalWatcher.Created += refreshHandler;
            _externalWatcher.Changed += refreshHandler;
            _externalWatcher.Deleted += refreshHandler;
            _externalWatcher.Renamed += renameHandler;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _externalWatcher?.Dispose();
            _externalWatcher = null;
            GC.SuppressFinalize(this);
        }
    }
}
