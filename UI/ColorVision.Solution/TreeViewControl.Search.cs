#pragma warning disable CA1868
using ColorVision.Solution.Editor;
using ColorVision.Solution.Explorer;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

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

            SearchStatusText.Text = "正在搜索…";
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
                SolutionSearchResult result = await SolutionSearchService.SearchAsync(
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
                SearchStatusText.Text = $"搜索失败：{ex.Message}";
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
                ? $"显示前 {_searchResultNodes.Count} 项，请继续输入以缩小范围"
                : $"找到 {_searchResultNodes.Count} 项";
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
                    targetNode = SolutionNodeFactory.CreateFolderNode(
                        new DirectoryInfo(hit.FullPath),
                        hit.Explorer);
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
            SolutionTreeView.ItemsSource = GetActiveWorkspaceItems();
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
            SearchBar1TextChanged();
        }

        private static void ExpandNodeAncestors(SolutionNode node)
        {
            for (SolutionNode? current = node.Parent; current != null; current = current.Parent)
                current.IsExpanded = true;
        }

        private void BringNodeIntoView(SolutionNode node)
        {
            var path = new Stack<SolutionNode>();
            for (SolutionNode? current = node; current != null; current = current.Parent)
                path.Push(current);

            ItemsControl parent = SolutionTreeView;
            TreeViewItem? container = null;
            SolutionTreeView.UpdateLayout();
            while (path.Count > 0)
            {
                SolutionNode pathNode = path.Pop();
                container = parent.ItemContainerGenerator.ContainerFromItem(pathNode) as TreeViewItem;
                if (container == null)
                {
                    parent.UpdateLayout();
                    container = parent.ItemContainerGenerator.ContainerFromItem(pathNode) as TreeViewItem;
                }
                if (container == null)
                    return;

                if (path.Count > 0)
                    container.IsExpanded = true;
                parent = container;
            }

            if (container == null)
                return;
            _allowProgrammaticBringIntoView = true;
            try
            {
                container.BringIntoView();
            }
            finally
            {
                _allowProgrammaticBringIntoView = false;
            }
        }

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            if (!_allowProgrammaticBringIntoView)
                e.Handled = true;
        }
    }
}
