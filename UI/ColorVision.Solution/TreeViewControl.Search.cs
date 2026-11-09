using LocalizedText = global::ColorVision.Solution.DisplayText;
#pragma warning disable CA1868
using ColorVision.Solution.Explorer;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Solution
{
    public partial class TreeViewControl
    {
        public void SearchBar1TextChanged()
        {
            if (SolutionTreeView == null || SearchStatusText == null)
                return;

            _searchDebounceTimer.Stop();
            if (string.IsNullOrWhiteSpace(SearchBar1.Text))
            {
                CancelPendingSearch();
                ShowSolutionTree();
                return;
            }

            SearchStatusText.Text = LocalizedText.Get("正在搜索…");
            SearchStatusText.Visibility = Visibility.Visible;
            _searchDebounceTimer.Start();
        }

        private async void SearchDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _searchDebounceTimer.Stop();
            string query = SearchBar1.Text.Trim();
            if (query.Length == 0)
            {
                ShowSolutionTree();
                return;
            }

            CancelPendingSearch();
            var cancellation = new CancellationTokenSource();
            _searchCancellation = cancellation;
            int version = ++_searchVersion;
            try
            {
                SolutionSearchResult result = IsFileSystemView
                    && GetDisplayedRootNode() is FileSystemFolderNode fileSystemRoot
                    && SolutionManager.CurrentSolutionExplorer is { } explorer
                    ? await SolutionSearchService.SearchFileSystemAsync(
                        explorer,
                        fileSystemRoot,
                        query,
                        SolutionSearchService.DefaultMaxResults,
                        cancellation.Token)
                    : await SolutionSearchService.SearchAsync(
                        GetActiveWorkspaceItems(),
                        query,
                        SolutionSearchService.DefaultMaxResults,
                        cancellation.Token);
                if (cancellation.IsCancellationRequested
                    || version != _searchVersion
                    || !string.Equals(query, SearchBar1.Text.Trim(), StringComparison.Ordinal))
                {
                    return;
                }

                ApplySearchResult(result);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                if (cancellation.IsCancellationRequested || version != _searchVersion)
                    return;
                SearchStatusText.Text = LocalizedText.Format($"搜索失败：{ex.Message}");
                SearchStatusText.Visibility = Visibility.Visible;
            }
            finally
            {
                if (ReferenceEquals(_searchCancellation, cancellation))
                    _searchCancellation = null;
                cancellation.Dispose();
            }
        }

        private void ApplySearchResult(SolutionSearchResult result)
        {
            _selectionService.Clear();
            SolutionTreeView.ItemsSource = null;
            DisposeSearchResultNodes();
            foreach (SolutionSearchHit hit in result.Hits)
            {
                if (!ReferenceEquals(hit.Explorer, SolutionManager.CurrentSolutionExplorer))
                    continue;
                SolutionNode? targetNode = CreateSearchTargetNode(hit, out bool ownsTarget);
                if (targetNode == null)
                    continue;
                _searchResultNodes.Add(new SolutionSearchResultNode(
                    hit.Explorer,
                    targetNode,
                    hit.DisplayPath,
                    ownsTarget));
            }

            SolutionTreeView.ItemsSource = _searchResultNodes;
            SearchStatusText.Text = result.IsTruncated
                ? LocalizedText.Format($"显示前 {_searchResultNodes.Count} 项，请继续输入以缩小范围")
                : LocalizedText.Format($"找到 {_searchResultNodes.Count} 项");
            SearchStatusText.Visibility = Visibility.Visible;
        }

        private static SolutionNode? CreateSearchTargetNode(
            SolutionSearchHit hit,
            out bool ownsTarget)
        {
            ownsTarget = false;
            if (hit.ExistingNode != null)
                return hit.ExistingNode;

            try
            {
                SolutionNode targetNode;
                if (hit.IsDirectory)
                {
                    if (!Directory.Exists(hit.FullPath))
                        return null;
                    targetNode = hit.ParentNode is FileSystemFolderNode
                        ? new FileSystemFolderNode(new DirectoryInfo(hit.FullPath))
                        : SolutionNodeFactory.CreateFolderNode(new DirectoryInfo(hit.FullPath), hit.Explorer);
                }
                else
                {
                    if (!File.Exists(hit.FullPath))
                        return null;
                    targetNode = SolutionNodeFactory.CreateFileNode(new FileInfo(hit.FullPath));
                }

                targetNode.Parent = hit.ParentNode;
                targetNode.UpdateProjectMembershipState();
                ownsTarget = true;
                return targetNode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"创建搜索结果节点失败: {hit.FullPath}, {ex}");
                return null;
            }
        }

        private void ShowSolutionTree()
        {
            _selectionService.Clear();
            SolutionTreeView.ItemsSource = GetDisplayedWorkspaceItems();
            DisposeSearchResultNodes();
            SearchStatusText.Text = string.Empty;
            SearchStatusText.Visibility = Visibility.Collapsed;
        }

        private void CancelPendingSearch()
        {
            _searchVersion++;
            _searchCancellation?.Cancel();
            _searchCancellation = null;
        }

        private void CancelPendingReveal()
        {
            _revealCancellation?.Cancel();
            _revealCancellation = null;
        }

        private void DisposeSearchResultNodes()
        {
            foreach (SolutionSearchResultNode searchResultNode in _searchResultNodes)
                searchResultNode.Dispose();
            _searchResultNodes.Clear();
        }

        private void SearchBar1_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isClearingSearchForReveal)
                CancelPendingReveal();
            if (!string.IsNullOrWhiteSpace(SearchBar1?.Text))
                CancelPendingSearch();
            SearchBar1TextChanged();
        }

        private static void ExpandNodeAncestors(SolutionNode node)
        {
            for (SolutionNode? current = node.Parent; current != null; current = current.Parent)
                current.IsExpanded = true;
        }

        internal void BringNodeIntoView(SolutionNode node)
        {
            var path = new Stack<SolutionNode>();
            for (SolutionNode? current = node; current != null; current = current.Parent)
                path.Push(current);

            _allowProgrammaticBringIntoView = true;
            try
            {
                ItemsControl parent = SolutionTreeView;
                SolutionTreeView.UpdateLayout();
                while (path.TryPop(out SolutionNode? pathNode))
                {
                    TreeViewItem? container = GetOrRealizeContainer(parent, pathNode);
                    if (container == null)
                        return;

                    if (path.Count == 0)
                        container.BringIntoView();
                    else
                        container.SetCurrentValue(TreeViewItem.IsExpandedProperty, true);
                    parent = container;
                }
            }
            finally
            {
                _allowProgrammaticBringIntoView = false;
            }
        }

        private static TreeViewItem? GetOrRealizeContainer(ItemsControl owner, SolutionNode node)
        {
            if (owner.ItemContainerGenerator.ContainerFromItem(node) is TreeViewItem container)
                return container;

            owner.UpdateLayout();
            if (owner.ItemContainerGenerator.ContainerFromItem(node) is TreeViewItem realized)
                return realized;

            int index = owner.Items.IndexOf(node);
            if (index < 0 || FindItemsHost(owner) is not { } panel)
                return null;

            panel.BringIndexIntoViewPublic(index);
            return owner.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItem;
        }

        private static VirtualizingStackPanel? FindItemsHost(DependencyObject parent)
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is VirtualizingStackPanel { IsItemsHost: true } panel)
                    return panel;
                if (child is not ItemsControl && FindItemsHost(child) is { } nestedPanel)
                    return nestedPanel;
            }
            return null;
        }

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            if (!_allowProgrammaticBringIntoView)
                e.Handled = true;
        }
    }
}
