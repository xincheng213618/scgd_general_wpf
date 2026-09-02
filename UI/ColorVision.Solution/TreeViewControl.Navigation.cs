using AvalonDock.Layout;
using ColorVision.Solution.Explorer;
using ColorVision.Solution.Workspace;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.Solution
{
    public partial class TreeViewControl
    {
        private void InitializeExplorerNavigation()
        {
            RegisterCommand(SolutionNavigationCommands.SyncWithActiveDocument, ExecuteSyncWithActiveDocument, CanExecuteSyncWithActiveDocument);
            RegisterCommand(SolutionNavigationCommands.CollapseAll, ExecuteCollapseAll, CanExecuteExplorerNavigation);
            RegisterCommand(SolutionNavigationCommands.Refresh, ExecuteExplorerRefresh, CanExecuteExplorerNavigation);
        }

        private void CanExecuteExplorerNavigation(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = GetDisplayedRootNode() is { } root
                && (e.Command != SolutionNavigationCommands.Refresh || root.CanRefresh);
            e.Handled = true;
        }

        private void ExecuteCollapseAll(object sender, ExecutedRoutedEventArgs e)
        {
            e.Handled = true;
            if (GetDisplayedRootNode() is not { } root)
                return;

            CancelWorkspaceStateRestore();
            CancelPendingReveal();
            SearchBar1.Text = string.Empty;
            SolutionTreeNavigationService.CollapseDescendants(root);
            _selectionService.SelectSingle(root);
            ClearTreeViewSelection();
            ScheduleWorkspaceStateSave();
            _ = Dispatcher.BeginInvoke(() => BringNodeIntoView(root), DispatcherPriority.Loaded);
        }

        private void ExecuteExplorerRefresh(object sender, ExecutedRoutedEventArgs e)
        {
            e.Handled = true;
            if (GetDisplayedRootNode() is not { CanRefresh: true } root)
                return;

            CancelWorkspaceStateRestore();
            CancelPendingReveal();
            try
            {
                if (root is not SolutionExplorer)
                {
                    ClearSelection();
                    if (string.IsNullOrWhiteSpace(SearchBar1.Text))
                        _selectionService.SelectSingle(root);
                }
                root.Refresh();
                if (!string.IsNullOrWhiteSpace(SearchBar1.Text))
                    SearchBar1TextChanged();
            }
            catch (Exception ex)
            {
                SearchStatusText.Text = $"刷新失败：{ex.Message}";
                SearchStatusText.Visibility = Visibility.Visible;
            }
        }

        private void CanExecuteSyncWithActiveDocument(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = GetDisplayedRootNode() is { } root
                && GetActiveDocumentPath() is { } path
                && SolutionTreeNavigationService.CanResolvePath(root, path);
            e.Handled = true;
        }

        private async void ExecuteSyncWithActiveDocument(object sender, ExecutedRoutedEventArgs e)
        {
            e.Handled = true;
            if (GetDisplayedRootNode() is not { } root || GetActiveDocumentPath() is not { } path)
                return;

            CancelWorkspaceStateRestore();
            CancelPendingReveal();
            var cancellation = new CancellationTokenSource();
            _revealCancellation = cancellation;
            try
            {
                SolutionNode? node = await SolutionTreeNavigationService.ResolvePathAsync(root, path, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(root, GetDisplayedRootNode()))
                    return;
                if (node == null)
                {
                    SearchStatusText.Text = "当前文档不在此资源管理器视图中";
                    SearchStatusText.Visibility = Visibility.Visible;
                    return;
                }

                _isRestoringWorkspaceState = true;
                _isClearingSearchForReveal = true;
                try
                {
                    SearchBar1.Text = string.Empty;
                    ExpandNodeAncestors(node);
                    _selectionService.SelectSingle(node);
                    ClearTreeViewSelection();
                    SearchStatusText.Text = string.Empty;
                    SearchStatusText.Visibility = Visibility.Collapsed;
                }
                finally
                {
                    _isClearingSearchForReveal = false;
                    _isRestoringWorkspaceState = false;
                }
                ScheduleWorkspaceStateSave();
                await Dispatcher.InvokeAsync(
                    () =>
                    {
                        if (ReferenceEquals(root, GetDisplayedRootNode()))
                            BringNodeIntoView(node);
                    },
                    DispatcherPriority.Loaded,
                    cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                SearchStatusText.Text = $"定位失败：{ex.Message}";
                SearchStatusText.Visibility = Visibility.Visible;
            }
            finally
            {
                if (ReferenceEquals(_revealCancellation, cancellation))
                    _revealCancellation = null;
                cancellation.Dispose();
            }
        }

        private static string? GetActiveDocumentPath()
        {
            object? layout = WorkspaceManager.layoutRoot ?? (object?)WorkspaceManager.LayoutDocumentPane;
            if (layout == null)
                return null;

            LayoutDocument? document = WorkspaceManager.FindDocumentActive(layout)
                ?? FindSelectedDocument(layout, WorkspaceManager.SelectedContentId)
                ?? FindSelectedDocument(layout, null);
            if (EditorDocumentService.TryGetFilePath(document, out string path))
                return path;

            // Legacy document providers use a physical path as ContentId.
            string? contentId = document?.ContentId;
            return !string.IsNullOrWhiteSpace(contentId)
                && Path.IsPathFullyQualified(contentId)
                && (File.Exists(contentId) || Directory.Exists(contentId))
                    ? contentId
                    : null;
        }

        private static LayoutDocument? FindSelectedDocument(object parent, string? preferredPath)
        {
            if (parent is not ILayoutContainer container)
                return null;
            foreach (ILayoutElement child in container.Children)
            {
                if (child is LayoutDocument { IsSelected: true } document
                    && (preferredPath == null
                        || string.Equals(document.ContentId, preferredPath, StringComparison.OrdinalIgnoreCase)
                        || (EditorDocumentService.TryGetFilePath(document, out string path)
                            && string.Equals(path, preferredPath, StringComparison.OrdinalIgnoreCase))))
                {
                    return document;
                }
                if (FindSelectedDocument(child, preferredPath) is { } selected)
                    return selected;
            }
            return null;
        }
    }
}
