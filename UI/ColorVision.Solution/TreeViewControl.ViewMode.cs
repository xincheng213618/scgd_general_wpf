using ColorVision.Solution.Explorer;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.Solution
{
    public partial class TreeViewControl
    {
        private FileSystemFolderNode? _fileSystemRoot;
        private SolutionExplorer? _fileSystemExplorer;

        public bool IsFileSystemView { get; private set; }

        private void InitializeExplorerViewMode()
        {
            UpdateExplorerViewHeader();
        }

        private IReadOnlyList<SolutionNode> GetDisplayedWorkspaceItems()
        {
            SolutionExplorer? explorer = SolutionManager.CurrentSolutionExplorer;
            if (!ReferenceEquals(_fileSystemExplorer, explorer))
                DisposeFileSystemView();
            UpdateExplorerViewHeader();
            if (explorer == null)
                return [];
            if (!IsFileSystemView)
                return [explorer];

            if (_fileSystemRoot == null)
            {
                _fileSystemExplorer = explorer;
                _fileSystemRoot = new FileSystemFolderNode(
                    new DirectoryInfo(explorer.DirectoryInfo.FullName),
                    isWorkspaceRoot: true)
                {
                    IsExpanded = true,
                };
            }
            return [_fileSystemRoot];
        }

        private SolutionNode? GetDisplayedRootNode()
        {
            SolutionExplorer? explorer = SolutionManager.CurrentSolutionExplorer;
            return IsFileSystemView
                ? ReferenceEquals(_fileSystemExplorer, explorer) ? _fileSystemRoot : null
                : explorer;
        }

        private void SolutionViewButton_Click(object sender, RoutedEventArgs e) => SetExplorerViewMode(false);

        private void FileSystemViewButton_Click(object sender, RoutedEventArgs e) => SetExplorerViewMode(true);

        private void SetExplorerViewMode(bool fileSystemView)
        {
            if (IsFileSystemView == fileSystemView)
                return;

            SaveWorkspaceState(SolutionManager.CurrentSolutionExplorer);
            _workspaceStateSaveTimer.Stop();
            _searchDebounceTimer.Stop();
            CancelWorkspaceStateRestore();
            CancelPendingSearch();
            CancelPendingReveal();
            ClearDropTargetVisual();
            _selectionService.Clear();
            ClearTreeViewSelection();
            DisposeSearchResultNodes();
            IsFileSystemView = fileSystemView;
            SolutionTreeView.ItemsSource = GetDisplayedWorkspaceItems();
            if (!string.IsNullOrWhiteSpace(SearchBar1.Text))
                SearchBar1TextChanged();
            else if (!IsFileSystemView)
                Dispatcher.BeginInvoke(
                    () => RestoreWorkspaceState(SolutionManager.CurrentSolutionExplorer),
                    DispatcherPriority.Loaded);
            CommandManager.InvalidateRequerySuggested();
        }

        private void UpdateExplorerViewHeader()
        {
            if (WorkspacePathText != null)
                WorkspacePathText.Text = SolutionManager.CurrentSolutionExplorer?.DirectoryInfo.FullName ?? "未打开工作区";
            if (SolutionViewButton != null)
                SolutionViewButton.IsChecked = !IsFileSystemView;
            if (FileSystemViewButton != null)
                FileSystemViewButton.IsChecked = IsFileSystemView;
        }

        private void DisposeFileSystemView()
        {
            _fileSystemRoot?.Dispose();
            _fileSystemRoot = null;
            _fileSystemExplorer = null;
        }
    }
}
