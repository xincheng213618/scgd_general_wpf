#pragma warning disable CA1868
using ColorVision.Solution.Editor;
using ColorVision.Solution.Explorer;
using System.Collections.Specialized;
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
    public class TreeViewItemMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double num = 0.0;
            UIElement uIElement = value as TreeViewItem;
            while (uIElement != null && uIElement.GetType() != typeof(TreeView))
            {
                uIElement = (UIElement)VisualTreeHelper.GetParent(uIElement);
                if (uIElement is TreeViewItem)
                {
                    num += 12.0;
                }
            }

            return new Thickness(num, 0.0, 0.0, 0.0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// TreeViewControl.xaml 的交互逻辑
    /// </summary>
    public partial class TreeViewControl : UserControl
    {
        private const string SolutionOrganizationDragFormat = "ColorVision.Solution.OrganizationNodes";

        public static SolutionManager SolutionManager => SolutionManager.GetInstance();

        private readonly SolutionContextMenuService _contextMenuService;
        private readonly SolutionSelectionService _selectionService;
        private readonly DispatcherTimer _workspaceStateSaveTimer;
        private readonly DispatcherTimer _searchDebounceTimer;
        private readonly HashSet<SolutionExplorer> _observedExplorers = new();
        private readonly List<SolutionSearchResultNode> _searchResultNodes = new();
        private SolutionNode? _dropTargetNode;
        private CancellationTokenSource? _workspaceStateRestoreCancellation;
        private CancellationTokenSource? _searchCancellation;
        private CancellationTokenSource? _revealCancellation;
        private int _searchVersion;
        private bool _isDragging;
        private bool _isRestoringWorkspaceState;
        private bool _isClearingSearchForReveal;
        private bool _allowProgrammaticBringIntoView;
        private bool _suppressTreeSelectionSynchronization;

        public IReadOnlyList<SolutionNode> SelectedNodes => _selectionService.CommandNodes;

        public TreeViewControl()
        {
            _selectionService = new SolutionSelectionService();
            _contextMenuService = new SolutionContextMenuService();
            _workspaceStateSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350),
            };
            _workspaceStateSaveTimer.Tick += WorkspaceStateSaveTimer_Tick;
            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(220),
            };
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
            _selectionService.SelectionChanged += SelectionService_SelectionChanged;
            InitializeComponent();
            _contextMenuService.GetSelectedNodes = () => _selectionService.SelectedNodes;
            _contextMenuService.CommandTarget = SolutionTreeView;
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = SolutionManager;
            SolutionTreeView.ItemsSource = SolutionManager.SolutionExplorers;
            IniCommand();
        }

        private async void TreeViewControl_Drop(object sender, DragEventArgs e)
        {
            SolutionNode? targetNode = GetNodeAtPoint(e.GetPosition(SolutionTreeView))?.ResolveCommandTarget();
            ClearDropTargetVisual();
            if (TryGetSolutionOrganizationDragData(e.Data, out SolutionOrganizationDragData? organizationData))
            {
                if (organizationData != null
                    && TryGetSolutionOrganizationTarget(targetNode, out SolutionExplorer? targetExplorer, out string? targetFolderId)
                    && ReferenceEquals(organizationData.SolutionExplorer, targetExplorer))
                {
                    if (!targetExplorer.MoveSolutionItemsToFolder(
                        organizationData.Projects,
                        organizationData.SolutionFolderIds,
                        organizationData.SolutionItemIds,
                        targetFolderId,
                        out string errorMessage))
                    {
                        ShowDropError(errorMessage);
                    }
                    else
                    {
                        if (targetNode is SolutionFolderNode targetFolder)
                            targetFolder.IsExpanded = true;
                        _selectionService.SelectMany(organizationData.DraggedNodes);
                        e.Effects = DragDropEffects.Move;
                    }
                }
                e.Handled = true;
                return;
            }

            if (TryGetDropPaths(e.Data, out var paths, out bool isInternalDrag))
            {
                if (!isInternalDrag && ContainsDroppedSolutionFile(paths))
                {
                    e.Handled = true;
                    await TryOpenDroppedSolutionFileAsync(paths);
                    return;
                }

                if (TryGetSolutionOrganizationTarget(
                    targetNode,
                    out SolutionExplorer? resourceTargetExplorer,
                    out string? resourceTargetFolderId)
                    && resourceTargetExplorer != null)
                {
                    if (!resourceTargetExplorer.RegisterDroppedSolutionResources(
                        paths,
                        resourceTargetFolderId,
                        out IReadOnlyList<SolutionNode> registeredNodes,
                        out string errorMessage))
                    {
                        ShowDropError(errorMessage);
                    }
                    else
                    {
                        if (targetNode is SolutionFolderNode targetFolder)
                            targetFolder.IsExpanded = true;
                        _selectionService.SelectMany(registeredNodes);
                        e.Effects = DragDropEffects.Copy;
                    }
                    e.Handled = true;
                    return;
                }

                if (!isInternalDrag && ContainsDroppedProject(paths))
                {
                    e.Handled = true;
                    await TryOpenDroppedProjectAsync(paths);
                    return;
                }

                string? targetDir = GetDropTargetDirectory(e);
                if (targetDir == null)
                    return;

                bool isMove = isInternalDrag
                    ? e.AllowedEffects.HasFlag(DragDropEffects.Move)
                        && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                    : Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                SolutionFileOperationResult result = SolutionClipboardFileOperations.Execute(
                    paths,
                    targetDir,
                    isMove);
                if (result.Failures.Count > 0)
                    ShowFileOperationFailures(result, isMove ? "移动失败" : "复制失败");
                e.Effects = result.SucceededCount > 0
                    ? isMove ? DragDropEffects.Move : DragDropEffects.Copy
                    : DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void TreeViewControl_DragOver(object sender, DragEventArgs e)
        {
            SolutionNode? targetNode = GetNodeAtPoint(e.GetPosition(SolutionTreeView))?.ResolveCommandTarget();
            if (TryGetSolutionOrganizationDragData(e.Data, out SolutionOrganizationDragData? organizationData))
            {
                SolutionExplorer? targetExplorer = null;
                bool canMove = organizationData != null
                    && TryGetSolutionOrganizationTarget(targetNode, out targetExplorer, out string? targetFolderId)
                    && ReferenceEquals(organizationData.SolutionExplorer, targetExplorer)
                    && targetExplorer.CanMoveSolutionItemsToFolder(
                        organizationData.Projects,
                        organizationData.SolutionFolderIds,
                        organizationData.SolutionItemIds,
                        targetFolderId,
                        out _);
                e.Effects = canMove ? DragDropEffects.Move : DragDropEffects.None;
                SetDropTargetVisual(canMove ? targetNode ?? targetExplorer : null);
                e.Handled = true;
                return;
            }

            if (TryGetDropPaths(e.Data, out string[] dropPaths, out bool isInternalDrag))
            {
                if (!isInternalDrag && ContainsDroppedSolutionFile(dropPaths))
                {
                    e.Effects = DragDropEffects.Copy;
                    ClearDropTargetVisual();
                    e.Handled = true;
                    return;
                }
                if (TryGetSolutionOrganizationTarget(
                    targetNode,
                    out SolutionExplorer? resourceTargetExplorer,
                    out _))
                {
                    e.Effects = resourceTargetExplorer?.CanModifySolutionStructure == true
                        && CanRegisterDroppedSolutionResources(dropPaths)
                        ? DragDropEffects.Copy
                        : DragDropEffects.None;
                    SetDropTargetVisual(e.Effects == DragDropEffects.None
                        ? null
                        : targetNode ?? resourceTargetExplorer);
                    e.Handled = true;
                    return;
                }

                bool isMove = isInternalDrag
                    ? e.AllowedEffects.HasFlag(DragDropEffects.Move)
                        && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                    : Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                DragDropEffects requestedEffect = isMove ? DragDropEffects.Move : DragDropEffects.Copy;
                SolutionNode? physicalTarget = GetPhysicalDropTargetNode(targetNode);
                e.Effects = physicalTarget != null && e.AllowedEffects.HasFlag(requestedEffect)
                    ? requestedEffect
                    : DragDropEffects.None;
                SetDropTargetVisual(e.Effects == DragDropEffects.None ? null : physicalTarget);
            }
            else
            {
                e.Effects = DragDropEffects.None;
                ClearDropTargetVisual();
            }
            e.Handled = true;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            AllowDrop = true;
            DragOver -= TreeViewControl_DragOver;
            Drop -= TreeViewControl_Drop;
            DragLeave -= TreeViewControl_DragLeave;
            DragOver += TreeViewControl_DragOver;
            Drop += TreeViewControl_Drop;
            DragLeave += TreeViewControl_DragLeave;

            // Use the shared context menu service instead of per-node ContextMenu
            SolutionTreeView.ContextMenu = _contextMenuService.ContextMenu;
            SolutionManager.SolutionExplorers.CollectionChanged -= SolutionExplorers_CollectionChanged;
            SolutionManager.SolutionExplorers.CollectionChanged += SolutionExplorers_CollectionChanged;
            SynchronizeObservedExplorers();
            Dispatcher.BeginInvoke(
                () => RestoreWorkspaceState(SolutionManager.CurrentSolutionExplorer),
                DispatcherPriority.Loaded);
            if (!string.IsNullOrWhiteSpace(SearchBar1.Text))
                SearchBar1TextChanged();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            SaveWorkspaceState(SolutionManager.CurrentSolutionExplorer);
            CancelWorkspaceStateRestore();
            _workspaceStateSaveTimer.Stop();
            _searchDebounceTimer.Stop();
            CancelPendingSearch();
            CancelPendingReveal();
            DisposeSearchResultNodes();
            SolutionManager.SolutionExplorers.CollectionChanged -= SolutionExplorers_CollectionChanged;
            foreach (SolutionExplorer explorer in _observedExplorers.ToList())
                DetachExplorer(explorer);
            _observedExplorers.Clear();
            DragOver -= TreeViewControl_DragOver;
            Drop -= TreeViewControl_Drop;
            DragLeave -= TreeViewControl_DragLeave;
            ClearDropTargetVisual();
        }

        private void SolutionExplorers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            _workspaceStateSaveTimer.Stop();
            CancelWorkspaceStateRestore();
            CancelPendingReveal();
            _isRestoringWorkspaceState = true;
            try
            {
                _selectionService.Clear();
            }
            finally
            {
                _isRestoringWorkspaceState = false;
            }

            SynchronizeObservedExplorers();
            Dispatcher.BeginInvoke(
                () => RestoreWorkspaceState(SolutionManager.CurrentSolutionExplorer),
                DispatcherPriority.Loaded);
            if (!string.IsNullOrWhiteSpace(SearchBar1.Text))
                SearchBar1TextChanged();
        }

        private void SynchronizeObservedExplorers()
        {
            var currentExplorers = SolutionManager.SolutionExplorers.ToHashSet();
            foreach (SolutionExplorer explorer in _observedExplorers.Except(currentExplorers).ToList())
            {
                DetachExplorer(explorer);
                _observedExplorers.Remove(explorer);
            }

            foreach (SolutionExplorer explorer in currentExplorers.Except(_observedExplorers))
            {
                AttachExplorer(explorer);
                _observedExplorers.Add(explorer);
            }
        }

        private void AttachExplorer(SolutionExplorer explorer)
        {
            explorer.VisualChildrenEventHandler += Explorer_VisualChildrenChanged;
            explorer.SolutionStateReloading += Explorer_SolutionStateReloading;
            explorer.SolutionStateReloaded += Explorer_SolutionStateReloaded;
            explorer.Disposing += Explorer_Disposing;
        }

        private void DetachExplorer(SolutionExplorer explorer)
        {
            explorer.VisualChildrenEventHandler -= Explorer_VisualChildrenChanged;
            explorer.SolutionStateReloading -= Explorer_SolutionStateReloading;
            explorer.SolutionStateReloaded -= Explorer_SolutionStateReloaded;
            explorer.Disposing -= Explorer_Disposing;
        }

        private void Explorer_VisualChildrenChanged(object? sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(SearchBar1.Text))
                SearchBar1TextChanged();
        }

        private void Explorer_SolutionStateReloading(object? sender, EventArgs e)
        {
            if (sender is SolutionExplorer explorer
                && ReferenceEquals(explorer, SolutionManager.CurrentSolutionExplorer))
            {
                _workspaceStateSaveTimer.Stop();
                SaveWorkspaceState(explorer);
                CancelWorkspaceStateRestore();
                CancelPendingReveal();
            }
        }

        private void Explorer_SolutionStateReloaded(object? sender, EventArgs e)
        {
            if (sender is not SolutionExplorer explorer
                || !ReferenceEquals(explorer, SolutionManager.CurrentSolutionExplorer))
            {
                return;
            }

            _workspaceStateSaveTimer.Stop();
            Dispatcher.BeginInvoke(() => RestoreWorkspaceState(explorer), DispatcherPriority.Loaded);
            if (!string.IsNullOrWhiteSpace(SearchBar1.Text))
                SearchBar1TextChanged();
        }

        private void Explorer_Disposing(object? sender, EventArgs e)
        {
            if (sender is SolutionExplorer explorer
                && ReferenceEquals(explorer, SolutionManager.CurrentSolutionExplorer))
            {
                _workspaceStateSaveTimer.Stop();
                SaveWorkspaceState(explorer);
                CancelWorkspaceStateRestore();
                CancelPendingReveal();
            }
        }

        private void SelectionService_SelectionChanged(object? sender, EventArgs e)
        {
            ScheduleWorkspaceStateSave();
            CommandManager.InvalidateRequerySuggested();
        }

        private void SolutionTreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (!_contextMenuService.PrepareMenu())
                e.Handled = true;
        }

        private void SolutionTreeView_SelectedItemChanged(
            object sender,
            RoutedPropertyChangedEventArgs<object> e)
        {
            if (_suppressTreeSelectionSynchronization
                || e.NewValue is not SolutionNode node)
            {
                return;
            }
            if (_selectionService.SelectedNodes is [var selected]
                && ReferenceEquals(selected, node))
            {
                return;
            }

            _selectionService.SelectSingle(node);
        }

        private void TreeViewItem_ExpansionChanged(object sender, RoutedEventArgs e)
        {
            ScheduleWorkspaceStateSave();
        }

        private void ScheduleWorkspaceStateSave()
        {
            if (_isRestoringWorkspaceState || !IsLoaded)
                return;

            _workspaceStateSaveTimer.Stop();
            _workspaceStateSaveTimer.Start();
        }

        private void WorkspaceStateSaveTimer_Tick(object? sender, EventArgs e)
        {
            _workspaceStateSaveTimer.Stop();
            SaveWorkspaceState(SolutionManager.CurrentSolutionExplorer);
        }

        private void SaveWorkspaceState(SolutionExplorer? explorer)
        {
            if (_isRestoringWorkspaceState || explorer == null)
                return;

            try
            {
                SolutionWorkspaceState state = SolutionWorkspaceStateStore.Capture(
                    explorer,
                    _selectionService.CommandNodes,
                    _selectionService.AnchorNode?.ResolveCommandTarget());
                SolutionWorkspaceStateStore.Save(explorer.ConfigFileInfo.FullName, state);
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
            {
                Debug.WriteLine($"保存解决方案工作区状态失败: {ex.Message}");
            }
        }

        private async void RestoreWorkspaceState(SolutionExplorer? explorer)
        {
            if (explorer == null
                || !ReferenceEquals(explorer, SolutionManager.CurrentSolutionExplorer))
            {
                return;
            }

            CancelWorkspaceStateRestore();
            var cancellation = new CancellationTokenSource();
            _workspaceStateRestoreCancellation = cancellation;
            SolutionWorkspaceStateLoadResult loadResult = SolutionWorkspaceStateStore.Load(
                explorer.ConfigFileInfo.FullName);
            _isRestoringWorkspaceState = true;
            try
            {
                _selectionService.Clear();
                if (!loadResult.HasPersistedState)
                    return;

                await SolutionWorkspaceStateStore.RestoreExpansionAsync(
                    explorer,
                    loadResult.State,
                    cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(explorer, SolutionManager.CurrentSolutionExplorer))
                    return;
                IReadOnlyList<SolutionNode> selectedNodes = SolutionWorkspaceStateStore.ResolveSelectedNodes(
                    explorer,
                    loadResult.State);
                SolutionNode? anchorNode = SolutionWorkspaceStateStore.ResolveAnchorNode(
                    explorer,
                    loadResult.State);
                _selectionService.SelectMany(selectedNodes, anchorNode);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"恢复解决方案工作区状态失败: {ex}");
            }
            finally
            {
                if (ReferenceEquals(_workspaceStateRestoreCancellation, cancellation))
                {
                    _workspaceStateRestoreCancellation = null;
                    _isRestoringWorkspaceState = false;
                    cancellation.Dispose();
                }
            }
        }

        private void CancelWorkspaceStateRestore()
        {
            _workspaceStateRestoreCancellation?.Cancel();
            _workspaceStateRestoreCancellation?.Dispose();
            _workspaceStateRestoreCancellation = null;
            _isRestoringWorkspaceState = false;
        }

        private void TreeViewControl_DragLeave(object sender, DragEventArgs e)
        {
            Point position = e.GetPosition(this);
            if (position.X < 0
                || position.Y < 0
                || position.X > ActualWidth
                || position.Y > ActualHeight)
            {
                ClearDropTargetVisual();
            }
        }

        private Point SelectPoint;

        private TreeViewItem? SelectedTreeViewItem;

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);

            SelectPoint = e.GetPosition(SolutionTreeView);
            HitTestResult result = VisualTreeHelper.HitTest(SolutionTreeView, SelectPoint);
            if (result != null)
            {
                TreeViewItem? item = ViewHelper.FindVisualParent<TreeViewItem>(result.VisualHit);
                if (item == null)
                {
                    if (e.ChangedButton is MouseButton.Left or MouseButton.Right)
                    {
                        ClearSelection();
                        e.Handled = e.ChangedButton == MouseButton.Right;
                    }
                    return;
                }

                if (item.DataContext is SolutionNode node)
                {
                    bool isCtrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
                    bool isShift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                    bool isRightClick = e.ChangedButton == MouseButton.Right;

                    if (isRightClick)
                    {
                        bool preserveMultiSelection = _selectionService.SelectedNodes.Contains(node);
                        _selectionService.PreserveOrSelectForContext(node);
                        ClearTreeViewSelection();
                        e.Handled = preserveMultiSelection;
                    }
                    else if (isShift)
                    {
                        _selectionService.SelectRange(GetVisibleNodes(), node, additive: isCtrl);
                        ClearTreeViewSelection();
                        e.Handled = true;
                    }
                    else if (isCtrl)
                    {
                        _selectionService.Toggle(node);
                        ClearTreeViewSelection();
                        e.Handled = true;
                    }
                    else
                    {
                        _selectionService.SelectSingle(node);
                    }

                    // Ensure TreeView has keyboard focus for Ctrl+C/V/X commands
                    if (!SolutionTreeView.IsKeyboardFocusWithin)
                        SolutionTreeView.Focus();
                }

                if (SelectedTreeViewItem != null && SelectedTreeViewItem != item && SelectedTreeViewItem.DataContext is SolutionNode vobj)
                {
                    vobj.IsEditMode = false;
                }
                SelectedTreeViewItem = item;
            }
            else
            {
                ClearSelection();
                SelectedTreeViewItem = null;
            }
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);

            if (_isDragging
                || e.LeftButton != MouseButtonState.Pressed
                || _selectionService.SelectedNodes.Count == 0)
                return;

            Point currentPoint = e.GetPosition(SolutionTreeView);
            if (Math.Abs(currentPoint.X - SelectPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(currentPoint.Y - SelectPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            IReadOnlyList<SolutionNode> selectedNodes = _selectionService.CommandNodes;
            if (TryCreateSolutionOrganizationDragData(selectedNodes, out SolutionOrganizationDragData? organizationData))
            {
                var organizationDataObject = new DataObject();
                organizationDataObject.SetData(SolutionOrganizationDragFormat, organizationData);
                RunDragDrop(organizationDataObject, DragDropEffects.Move);
                return;
            }

            if (selectedNodes.Any(node => !node.CanCopy || node.ClipboardResourcePath == null))
                return;

            IReadOnlyList<SolutionNode> physicalNodes = _selectionService.GetTopLevelNodes(node =>
                node.CanCopy && node.ClipboardResourcePath != null);
            var paths = physicalNodes
                .Select(node => node.ClipboardResourcePath!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (paths.Length == 0)
                return;

            DragDropEffects allowedEffects = physicalNodes.All(node => node.CanCut)
                ? DragDropEffects.Copy | DragDropEffects.Move
                : DragDropEffects.Copy;
            RunDragDrop(CreatePathDataObject(paths, isCut: false), allowedEffects);
        }

        private static bool TryCreateSolutionOrganizationDragData(
            IReadOnlyList<SolutionNode> selectedNodes,
            out SolutionOrganizationDragData? dragData)
        {
            dragData = null;
            if (selectedNodes.Count == 0
                || selectedNodes.Any(node => node is not ProjectNode and not SolutionFolderNode and not SolutionItemNode))
            {
                return false;
            }

            SolutionExplorer? solutionExplorer = selectedNodes[0] switch
            {
                ProjectNode projectNode => projectNode.SolutionExplorer,
                SolutionFolderNode folderNode => folderNode.SolutionExplorer,
                SolutionItemNode solutionItemNode => solutionItemNode.SolutionExplorer,
                _ => null,
            };
            if (solutionExplorer?.IsExplicitProjectMode != true
                || !solutionExplorer.CanModifySolutionStructure
                || selectedNodes.Any(node => !ReferenceEquals(solutionExplorer, node switch
                {
                    ProjectNode projectNode => projectNode.SolutionExplorer,
                    SolutionFolderNode folderNode => folderNode.SolutionExplorer,
                    SolutionItemNode solutionItemNode => solutionItemNode.SolutionExplorer,
                    _ => null,
                })))
            {
                return false;
            }

            var selectedSet = selectedNodes.ToHashSet();
            List<SolutionNode> topLevelNodes = selectedNodes
                .Where(node => !HasSelectedAncestor(node, selectedSet))
                .ToList();
            dragData = new SolutionOrganizationDragData(
                solutionExplorer,
                topLevelNodes,
                topLevelNodes.OfType<ProjectNode>().Select(node => node.Project).ToArray(),
                topLevelNodes.OfType<SolutionFolderNode>().Select(node => node.FolderId).ToArray(),
                topLevelNodes.OfType<SolutionItemNode>().Select(node => node.ItemId).ToArray());
            return dragData.Projects.Count > 0
                || dragData.SolutionFolderIds.Count > 0
                || dragData.SolutionItemIds.Count > 0;
        }

        private static bool HasSelectedAncestor(SolutionNode node, HashSet<SolutionNode> selectedNodes)
        {
            SolutionNode? parent = node.Parent;
            while (parent != null)
            {
                if (selectedNodes.Contains(parent))
                    return true;
                parent = parent.Parent;
            }
            return false;
        }

        private static bool TryGetSolutionOrganizationDragData(
            IDataObject dataObject,
            out SolutionOrganizationDragData? dragData)
        {
            dragData = dataObject.GetDataPresent(SolutionOrganizationDragFormat, autoConvert: false)
                ? dataObject.GetData(SolutionOrganizationDragFormat, autoConvert: false) as SolutionOrganizationDragData
                : null;
            return dragData != null;
        }

        private static bool TryGetSolutionOrganizationTarget(
            SolutionNode? targetNode,
            out SolutionExplorer? solutionExplorer,
            out string? solutionFolderId)
        {
            switch (targetNode)
            {
                case SolutionFolderNode solutionFolderNode:
                    solutionExplorer = solutionFolderNode.SolutionExplorer;
                    solutionFolderId = solutionFolderNode.FolderId;
                    return true;
                case SolutionExplorer root:
                    solutionExplorer = root;
                    solutionFolderId = null;
                    return true;
                case null when SolutionManager.CurrentSolutionExplorer is { } current:
                    solutionExplorer = current;
                    solutionFolderId = null;
                    return true;
                default:
                    solutionExplorer = null;
                    solutionFolderId = null;
                    return false;
            }
        }

        private static bool CanRegisterDroppedSolutionResources(IEnumerable<string> paths)
        {
            bool hasPaths = false;
            foreach (string path in paths)
            {
                hasPaths = true;
                if (File.Exists(path))
                {
                    if (SolutionManager.IsSolutionFilePath(path))
                        return false;
                    continue;
                }
                if (Directory.Exists(path)
                    && ProjectProviderRegistry.EnumerateProjectFiles(
                        new DirectoryInfo(path),
                        SearchOption.TopDirectoryOnly).Any())
                {
                    continue;
                }
                return false;
            }
            return hasPaths;
        }

        private static bool ContainsDroppedSolutionFile(IEnumerable<string> paths)
        {
            return paths.Any(path => File.Exists(path) && SolutionManager.IsSolutionFilePath(path));
        }

        private static bool ContainsDroppedProject(IEnumerable<string> paths)
        {
            return paths.Any(path =>
                File.Exists(path) && SolutionManager.IsProjectFilePath(path)
                || Directory.Exists(path)
                    && ProjectProviderRegistry.EnumerateProjectFiles(
                        new DirectoryInfo(path),
                        SearchOption.TopDirectoryOnly).Any());
        }

        private static void ShowDropError(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
                return;
            MessageBox.Show(
                Application.Current?.GetActiveWindow(),
                errorMessage,
                "ColorVision",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private sealed record SolutionOrganizationDragData(
            SolutionExplorer SolutionExplorer,
            IReadOnlyList<SolutionNode> DraggedNodes,
            IReadOnlyList<ProjectDefinition> Projects,
            IReadOnlyList<string> SolutionFolderIds,
            IReadOnlyList<string> SolutionItemIds);

        private static bool TryGetDropPaths(IDataObject dataObject, out string[] paths, out bool isInternalDrag)
        {
            paths = Array.Empty<string>();
            isInternalDrag = dataObject.GetDataPresent(ClipboardFormat);

            if (dataObject.GetDataPresent(DataFormats.FileDrop) &&
                dataObject.GetData(DataFormats.FileDrop) is string[] fileDropPaths)
            {
                paths = fileDropPaths
                    .Where(path => File.Exists(path) || Directory.Exists(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            return paths.Length > 0;
        }

        private static async Task<bool> TryOpenDroppedSolutionFileAsync(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                if (File.Exists(path) && SolutionManager.IsSolutionFilePath(path))
                {
                    ResourceOpenResult result = await ResourceOpenService.Instance.OpenAsync(path);
                    if (result.Succeeded)
                        return true;
                    if (!result.Canceled)
                        ShowDropError(result.ErrorMessage);
                    continue;
                }

            }

            return false;
        }

        private static async Task<bool> TryOpenDroppedProjectAsync(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {

                if (File.Exists(path) && SolutionManager.IsProjectFilePath(path))
                {
                    ResourceOpenResult result = await ResourceOpenService.Instance.OpenAsync(path);
                    if (result.Succeeded)
                        return true;
                    if (!result.Canceled)
                        ShowDropError(result.ErrorMessage);
                    continue;
                }

                if (!Directory.Exists(path))
                    continue;

                foreach (FileInfo projectFile in ProjectProviderRegistry.EnumerateProjectFiles(
                    new DirectoryInfo(path),
                    SearchOption.TopDirectoryOnly))
                {
                    ResourceOpenResult result = await ResourceOpenService.Instance.OpenAsync(projectFile.FullName);
                    if (result.Succeeded)
                        return true;
                    if (!result.Canceled)
                        ShowDropError(result.ErrorMessage);
                }
            }

            return false;
        }

        private string? GetDropTargetDirectory(DragEventArgs e)
        {
            SolutionNode? targetNode = GetPhysicalDropTargetNode(
                GetNodeAtPoint(e.GetPosition(SolutionTreeView)));
            return (targetNode as ISolutionPhysicalContainer)?.PhysicalContainerPath;
        }

        private static SolutionNode? GetPhysicalDropTargetNode(SolutionNode? targetNode)
        {
            if (targetNode == null)
            {
                SolutionExplorer? currentExplorer = SolutionManager.CurrentSolutionExplorer;
                return currentExplorer?.CanPaste == true ? currentExplorer : null;
            }

            targetNode = targetNode.ResolveCommandTarget();
            if (targetNode is ISolutionPhysicalContainer && targetNode.CanPaste)
                return targetNode;
            if (targetNode is FileNode
                && targetNode.Parent?.ResolveCommandTarget() is ISolutionPhysicalContainer parentContainer
                && targetNode.Parent.CanPaste
                && Directory.Exists(parentContainer.PhysicalContainerPath))
            {
                return targetNode.Parent;
            }
            return null;
        }

        private void RunDragDrop(IDataObject dataObject, DragDropEffects allowedEffects)
        {
            _isDragging = true;
            try
            {
                DragDrop.DoDragDrop(SolutionTreeView, dataObject, allowedEffects);
            }
            finally
            {
                _isDragging = false;
                ClearDropTargetVisual();
            }
        }

        private void SetDropTargetVisual(SolutionNode? node)
        {
            if (ReferenceEquals(_dropTargetNode, node))
                return;
            if (_dropTargetNode != null)
                _dropTargetNode.IsDropTarget = false;
            _dropTargetNode = node;
            if (_dropTargetNode != null)
                _dropTargetNode.IsDropTarget = true;
        }

        private void ClearDropTargetVisual()
        {
            SetDropTargetVisual(null);
        }

        private SolutionNode? GetNodeAtPoint(Point point)
        {
            HitTestResult result = VisualTreeHelper.HitTest(SolutionTreeView, point);
            if (result == null)
                return null;

            TreeViewItem? item = ViewHelper.FindVisualParent<TreeViewItem>(result.VisualHit);
            return item?.DataContext as SolutionNode;
        }

        private void ClearSelection()
        {
            _selectionService.Clear();
            ClearTreeViewSelection();
        }

        private List<SolutionNode> GetVisibleNodes()
        {
            var nodes = new List<SolutionNode>();
            foreach (SolutionNode rootNode in SolutionTreeView.Items.OfType<SolutionNode>())
                AppendVisibleNodes(rootNode, nodes);
            return nodes;
        }

        private static void AppendVisibleNodes(SolutionNode node, List<SolutionNode> nodes)
        {
            nodes.Add(node);
            if (!node.IsExpanded)
                return;

            foreach (SolutionNode child in node.VisualChildren)
                AppendVisibleNodes(child, nodes);
        }

        /// <summary>
        /// Clear TreeView's internal IsSelected state to prevent visual conflicts.
        /// We use IsMultiSelected exclusively for selection visuals.
        /// </summary>
        private void ClearTreeViewSelection()
        {
            if (SolutionTreeView.SelectedItem is not SolutionNode selected)
                return;

            _suppressTreeSelectionSynchronization = true;
            try
            {
                selected.IsSelected = false;
            }
            finally
            {
                _suppressTreeSelectionSynchronization = false;
            }
        }

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
                    SolutionManager.SolutionExplorers.ToList(),
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
                if (!SolutionManager.SolutionExplorers.Contains(hit.Explorer))
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
            SolutionTreeView.ItemsSource = SolutionManager.SolutionExplorers;
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
