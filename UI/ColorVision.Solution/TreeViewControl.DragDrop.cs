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
                    e.Effects = resourceTargetExplorer != null
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

    }
}
