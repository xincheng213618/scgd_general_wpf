#pragma warning disable CA1868
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
        private readonly HashSet<SolutionExplorer> _observedExplorers = new();
        private SolutionNode? _dropTargetNode;
        private CancellationTokenSource? _workspaceStateRestoreCancellation;
        private bool _isDragging;
        private bool _isRestoringWorkspaceState;

        public IReadOnlyList<SolutionNode> SelectedNodes => _selectionService.SelectedNodes;

        public TreeViewControl()
        {
            InitializeComponent();
            _selectionService = new SolutionSelectionService();
            _contextMenuService = new SolutionContextMenuService();
            _contextMenuService.GetSelectedNodes = () => _selectionService.SelectedNodes;
            _contextMenuService.CommandTarget = SolutionTreeView;
            _workspaceStateSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350),
            };
            _workspaceStateSaveTimer.Tick += WorkspaceStateSaveTimer_Tick;
            _selectionService.SelectionChanged += SelectionService_SelectionChanged;
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = SolutionManager;
            SolutionTreeView.ItemsSource = SolutionManager.SolutionExplorers;
            IniCommand();
        }

        private void TreeViewControl_Drop(object sender, DragEventArgs e)
        {
            SolutionNode? targetNode = GetNodeAtPoint(e.GetPosition(SolutionTreeView));
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
                if (!isInternalDrag && TryOpenDroppedSolutionFile(paths))
                {
                    e.Handled = true;
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

                if (!isInternalDrag && TryOpenDroppedProject(paths))
                {
                    e.Handled = true;
                    return;
                }

                string? targetDir = GetDropTargetDirectory(e);
                if (targetDir == null)
                    return;

                bool isMove = isInternalDrag
                    ? e.AllowedEffects.HasFlag(DragDropEffects.Move)
                        && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                    : Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                CopyOrMovePaths(paths, targetDir, isMove);
                e.Handled = true;
            }
        }

        private void TreeViewControl_DragOver(object sender, DragEventArgs e)
        {
            SolutionNode? targetNode = GetNodeAtPoint(e.GetPosition(SolutionTreeView));
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
                    e.Effects = CanRegisterDroppedSolutionResources(dropPaths)
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
                e.Effects = e.AllowedEffects.HasFlag(requestedEffect) ? requestedEffect : DragDropEffects.None;
                SetDropTargetVisual(e.Effects == DragDropEffects.None
                    ? null
                    : GetPhysicalDropTargetNode(targetNode));
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
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            SaveWorkspaceState(SolutionManager.CurrentSolutionExplorer);
            CancelWorkspaceStateRestore();
            _workspaceStateSaveTimer.Stop();
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
        }

        private void Explorer_Disposing(object? sender, EventArgs e)
        {
            if (sender is SolutionExplorer explorer
                && ReferenceEquals(explorer, SolutionManager.CurrentSolutionExplorer))
            {
                _workspaceStateSaveTimer.Stop();
                SaveWorkspaceState(explorer);
                CancelWorkspaceStateRestore();
            }
        }

        private void SelectionService_SelectionChanged(object? sender, EventArgs e)
        {
            ScheduleWorkspaceStateSave();
            CommandManager.InvalidateRequerySuggested();
        }

        private void SolutionTreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (_selectionService.SelectedNodes.Count == 0)
                e.Handled = true;
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
                    _selectionService.SelectedNodes,
                    _selectionService.AnchorNode);
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

                    if (isShift)
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
                    else if (isRightClick)
                    {
                        bool preserveMultiSelection = _selectionService.SelectedNodes.Contains(node);
                        _selectionService.PreserveOrSelectForContext(node);
                        ClearTreeViewSelection();
                        e.Handled = preserveMultiSelection;
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

            IReadOnlyList<SolutionNode> selectedNodes = _selectionService.SelectedNodes;
            if (TryCreateSolutionOrganizationDragData(selectedNodes, out SolutionOrganizationDragData? organizationData))
            {
                var organizationDataObject = new DataObject();
                organizationDataObject.SetData(SolutionOrganizationDragFormat, organizationData);
                RunDragDrop(organizationDataObject, DragDropEffects.Move);
                return;
            }

            if (selectedNodes.Any(node => !node.CanCopy))
                return;

            var paths = selectedNodes
                .Where(node => !string.IsNullOrWhiteSpace(node.FullPath) && (File.Exists(node.FullPath) || Directory.Exists(node.FullPath)))
                .Select(node => node.FullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (paths.Length == 0)
                return;

            DragDropEffects allowedEffects = selectedNodes.All(node => node.CanCut)
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

        private static bool TryOpenDroppedSolutionFile(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                if (File.Exists(path) && SolutionManager.IsSolutionFilePath(path))
                {
                    if (SolutionManager.OpenSolution(path))
                        return true;
                    continue;
                }

            }

            return false;
        }

        private static bool TryOpenDroppedProject(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {

                if (File.Exists(path) && SolutionManager.IsProjectFilePath(path))
                {
                    if (SolutionManager.OpenProject(path))
                        return true;
                    continue;
                }

                if (!Directory.Exists(path))
                    continue;

                foreach (FileInfo projectFile in ProjectProviderRegistry.EnumerateProjectFiles(
                    new DirectoryInfo(path),
                    SearchOption.TopDirectoryOnly))
                {
                    if (SolutionManager.OpenProject(projectFile.FullName))
                        return true;
                }
            }

            return false;
        }

        private string? GetDropTargetDirectory(DragEventArgs e)
        {
            var node = GetNodeAtPoint(e.GetPosition(SolutionTreeView));
            if (node is FileNode)
                return Path.GetDirectoryName(node.FullPath);

            if (node != null && Directory.Exists(node.FullPath))
                return node.FullPath;

            return SolutionManager.CurrentSolutionExplorer?.DirectoryInfo?.FullName;
        }

        private static SolutionNode? GetPhysicalDropTargetNode(SolutionNode? targetNode)
        {
            if (targetNode is FileNode)
                return targetNode.Parent ?? SolutionManager.CurrentSolutionExplorer;
            if (targetNode != null && Directory.Exists(targetNode.FullPath))
                return targetNode;
            return SolutionManager.CurrentSolutionExplorer;
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
            if (SolutionTreeView.SelectedItem is SolutionNode selected)
                selected.IsSelected = false;
        }

        private readonly char[] Chars = new[] { ' ' };

        public void SearchBar1TextChanged()
        {
            string text = SearchBar1.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                SolutionTreeView.ItemsSource = SolutionManager.GetInstance().SolutionExplorers;
            }
            else
            {
                var keywords = text.Split(Chars, StringSplitOptions.RemoveEmptyEntries);
                var currentExplorer = SolutionManager.GetInstance().CurrentSolutionExplorer;
                List<SolutionNode> filteredResults;
                if (currentExplorer?.Cache?.HasCache() == true)
                {
                    filteredResults = currentExplorer.Cache.Search(keywords)
                        .Select(CreateSearchResultNode)
                        .Where(node => node != null)
                        .Cast<SolutionNode>()
                        .ToList();
                }
                else
                {
                    filteredResults = SolutionManager.GetInstance().SolutionExplorers.
                        SelectMany(explorer => explorer.VisualChildren.GetAllVisualChildren())
                        .OfType<SolutionNode>()
                        .Where(template => keywords.All(keyword =>
                            template.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                            ))
                        .ToList();
                }

                SolutionTreeView.ItemsSource = filteredResults;
            }
        }

        private static SolutionNode? CreateSearchResultNode(FileTreeCacheEntry entry)
        {
            try
            {
                if (entry.IsDirectory)
                {
                    if (!Directory.Exists(entry.FullPath))
                        return null;
                    return SolutionNodeFactory.CreateFolderNode(
                        new DirectoryInfo(entry.FullPath),
                        SolutionManager.GetInstance().CurrentSolutionExplorer);
                }

                if (!File.Exists(entry.FullPath))
                    return null;
                return SolutionNodeFactory.CreateFileNode(new FileInfo(entry.FullPath));
            }
            catch
            {
                return null;
            }
        }

        private void SearchBar1_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchBar1TextChanged();
        }


        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }
    }
}
