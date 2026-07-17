using ColorVision.Solution.Explorer;
using ColorVision.UI;
using ColorVision.Solution.Workspace;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Solution
{
    public partial class TreeViewControl
    {
        private const string ClipboardFormat = "SolutionNodePath";
        private bool _isCutOperation;

        private void IniCommand()
        {
            RegisterCommand(ApplicationCommands.Copy, ExecutedCommand, CanExecuteCommand);
            RegisterCommand(ApplicationCommands.Cut, ExecutedCommand, CanExecuteCommand);
            RegisterCommand(ApplicationCommands.Paste, ExecutedCommand, CanExecuteCommand);
            RegisterCommand(ApplicationCommands.Delete, ExecuteDelete, CanExecuteDelete);
            RegisterCommand(Commands.ReName, ExecuteRename, CanExecuteRename);
            RegisterCommand(NavigationCommands.Refresh, ExecuteRefresh, CanExecuteRefresh);
            RegisterCommand(SolutionProjectCommands.Build, ExecuteProjectCapability, CanExecuteProjectCapability);
            RegisterCommand(SolutionProjectCommands.BuildSolution, ExecuteBuildSolution, CanExecuteBuildSolution);
            RegisterCommand(SolutionProjectCommands.Run, ExecuteProjectCapability, CanExecuteProjectCapability);
            RegisterCommand(SolutionProjectCommands.Debug, ExecuteProjectCapability, CanExecuteProjectCapability);
            RegisterCommand(SolutionProjectCommands.SetStartupProject, ExecuteSetStartupProject, CanExecuteSetStartupProject);
            RegisterCommand(SolutionProjectCommands.ExcludeFromProject, ExecuteProjectItemMembership, CanExecuteProjectItemMembership);
            RegisterCommand(SolutionProjectCommands.IncludeInProject, ExecuteProjectItemMembership, CanExecuteProjectItemMembership);
            RegisterCommand(SolutionNavigationCommands.RevealInTree, ExecuteRevealInTree, CanExecuteRevealInTree);
        }

        private void RegisterCommand(ICommand command, ExecutedRoutedEventHandler executed, CanExecuteRoutedEventHandler canExecute)
        {
            SolutionTreeView.CommandBindings.Add(new CommandBinding(command, executed, canExecute));
        }

        #region Command Handlers

        private void CanExecuteCommand(object sender, CanExecuteRoutedEventArgs e)
        {
            var selectedNodes = _selectionService.CommandNodes;
            if (selectedNodes.Count == 0)
            {
                e.CanExecute = false;
                e.Handled = true;
                return;
            }

            if (e.Command == ApplicationCommands.Copy)
            {
                e.CanExecute = selectedNodes.All(node => node.CanCopy && !string.IsNullOrEmpty(node.FullPath));
            }
            else if (e.Command == ApplicationCommands.Cut)
            {
                e.CanExecute = selectedNodes.All(node => node.CanCut && !string.IsNullOrEmpty(node.FullPath));
            }
            else if (e.Command == ApplicationCommands.Paste)
            {
                e.CanExecute = selectedNodes.Count == 1
                    && selectedNodes[0].CanPaste
                    && HasClipboardPaths();
            }
            e.Handled = true;
        }

        private void ExecutedCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command == ApplicationCommands.Copy)
            {
                string[] paths = GetSelectedPaths(node => node.CanCopy);
                if (paths.Length > 0)
                {
                    SetClipboardPaths(paths, isCut: false);
                    _isCutOperation = false;
                }
            }
            else if (e.Command == ApplicationCommands.Cut)
            {
                string[] paths = GetSelectedPaths(node => node.CanCut);
                if (paths.Length > 0)
                {
                    SetClipboardPaths(paths, isCut: true);
                    _isCutOperation = true;
                }
            }
            else if (e.Command == ApplicationCommands.Paste)
            {
                if (!TryGetClipboardPaths(out var sourcePaths, out bool isCut, out bool isInternalClipboard) || _selectionService.CommandNodes.Count == 0)
                    return;

                var targetNode = _selectionService.CommandNodes[0];
                string targetDir = targetNode.FullPath;
                if (targetNode is FileNode)
                    targetDir = Path.GetDirectoryName(targetNode.FullPath) ?? targetDir;

                if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
                    return;

                CopyOrMovePaths(sourcePaths, targetDir, isCut);

                if (isCut && isInternalClipboard)
                {
                    Clipboard.Clear();
                    _isCutOperation = false;
                }
            }
            e.Handled = true;
        }

        private string[] GetSelectedPaths(Func<SolutionNode, bool> capability)
        {
            return _selectionService.GetTopLevelNodes(node => capability(node) && !string.IsNullOrWhiteSpace(node.FullPath))
                .Select(node => node.FullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private void CanExecuteDelete(object sender, CanExecuteRoutedEventArgs e)
        {
            var selectedNodes = _selectionService.CommandNodes;
            e.CanExecute = selectedNodes.Count > 0 && selectedNodes.All(node => node.CanDelete);
            e.Handled = true;
        }

        private void ExecuteDelete(object sender, ExecutedRoutedEventArgs e)
        {
            var nodes = _selectionService.GetTopLevelNodes(node => node.CanDelete);
            if (nodes.Count == 0)
                return;

            if (_selectionService.CommandNodes.Count == 1)
            {
                if (!nodes[0].TryDelete(showConfirmation: true))
                    return;
            }
            else
            {
                string names = string.Join(Environment.NewLine, nodes.Take(5).Select(node => $"• {node.Name}"));
                if (nodes.Count > 5)
                    names += $"{Environment.NewLine}• …";

                var result = MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"确定删除或从解决方案移除选中的 {nodes.Count} 项吗？{Environment.NewLine}{Environment.NewLine}{names}",
                    "ColorVision",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.OK)
                    return;

                if (!EditorDocumentService.TryCloseDocumentsForResources(nodes.Select(node => node.FullPath)))
                    return;

                IReadOnlyList<SolutionNode> failedNodes = DeleteNodesByOwningSolution(nodes);
                if (failedNodes.Count > 0)
                {
                    MessageBox.Show(
                        Application.Current.GetActiveWindow(),
                        $"有 {failedNodes.Count} 项未能删除或移除，请查看前面的错误信息。",
                        "ColorVision",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }

            _selectionService.Clear();
            e.Handled = true;
        }

        internal static IReadOnlyList<SolutionNode> DeleteNodesByOwningSolution(
            IReadOnlyList<SolutionNode> nodes)
        {
            ArgumentNullException.ThrowIfNull(nodes);
            List<SolutionNode> distinctNodes = nodes.Distinct().ToList();
            var failedNodes = new HashSet<SolutionNode>();
            foreach (IGrouping<SolutionExplorer?, SolutionNode> group in distinctNodes
                .GroupBy(node => node.FindSolutionExplorer()))
            {
                IReadOnlyList<SolutionNode> groupFailures = group.Key != null
                    ? group.Key.DeleteNodesAsSingleOperation(group.ToList())
                    : SolutionBatchDeleteService.Delete(group.ToList());
                failedNodes.UnionWith(groupFailures);
            }
            return distinctNodes.Where(failedNodes.Contains).ToList();
        }

        private void CanExecuteRename(object sender, CanExecuteRoutedEventArgs e)
        {
            var selectedNodes = _selectionService.CommandNodes;
            e.CanExecute = selectedNodes.Count == 1 && selectedNodes[0].CanReName;
            e.Handled = true;
        }

        private void ExecuteRename(object sender, ExecutedRoutedEventArgs e)
        {
            if (_selectionService.SelectedNodes is [var visualNode]
                && _selectionService.CommandNodes is [var commandNode]
                && commandNode.CanReName)
            {
                visualNode.IsEditMode = true;
            }
            e.Handled = true;
        }

        private void CanExecuteRevealInTree(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _selectionService.SelectedNodes is [SolutionSearchResultNode searchResult]
                && SolutionManager.SolutionExplorers.Contains(searchResult.Explorer);
            e.Handled = true;
        }

        private async void ExecuteRevealInTree(object sender, ExecutedRoutedEventArgs e)
        {
            e.Handled = true;
            if (_selectionService.SelectedNodes is not [SolutionSearchResultNode searchResult])
                return;

            SolutionExplorer explorer = searchResult.Explorer;
            SolutionNode targetNode = searchResult.TargetNode;
            if (!SolutionManager.SolutionExplorers.Contains(explorer))
                return;

            CancelPendingReveal();
            var cancellation = new CancellationTokenSource();
            _revealCancellation = cancellation;
            try
            {
                SolutionNode? resolvedNode = await SolutionTreeNavigationService.ResolveNodeAsync(
                    explorer,
                    targetNode,
                    cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (resolvedNode == null || !SolutionManager.SolutionExplorers.Contains(explorer))
                {
                    SearchStatusText.Text = "无法在解决方案树中定位该项";
                    SearchStatusText.Visibility = Visibility.Visible;
                    return;
                }

                CancelWorkspaceStateRestore();
                _isRestoringWorkspaceState = true;
                try
                {
                    SolutionManager.CurrentSolutionExplorer = explorer;
                    _isClearingSearchForReveal = true;
                    SearchBar1.Text = string.Empty;
                    ExpandNodeAncestors(resolvedNode);
                    _selectionService.SelectSingle(resolvedNode);
                    ClearTreeViewSelection();
                }
                finally
                {
                    _isClearingSearchForReveal = false;
                    _isRestoringWorkspaceState = false;
                }

                ScheduleWorkspaceStateSave();
                _ = Dispatcher.BeginInvoke(
                    () => BringNodeIntoView(resolvedNode),
                    System.Windows.Threading.DispatcherPriority.Loaded);
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

        private void CanExecuteRefresh(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = GetSelectedSolutionExplorer() != null;
            e.Handled = true;
        }

        private void ExecuteRefresh(object sender, ExecutedRoutedEventArgs e)
        {
            GetSelectedSolutionExplorer()?.ReloadSolutionState();
            e.Handled = true;
        }

        private SolutionExplorer? GetSelectedSolutionExplorer()
        {
            if (_selectionService.CommandNodes is not [var selectedNode])
                return null;

            for (SolutionNode? current = selectedNode; current != null; current = current.Parent)
            {
                if (current is SolutionExplorer solutionExplorer)
                    return solutionExplorer;
            }
            return selectedNode switch
            {
                ProjectNode projectNode => projectNode.SolutionExplorer,
                UnavailableProjectNode unavailableProjectNode => unavailableProjectNode.SolutionExplorer,
                _ => null,
            };
        }

        private void CanExecuteProjectCapability(object sender, CanExecuteRoutedEventArgs e)
        {
            string? capabilityId = SolutionProjectCommands.GetCapabilityId(e.Command);
            e.CanExecute = capabilityId != null && _selectionService.CommandNodes switch
            {
                [ProjectNode projectNode] => projectNode.CanExecuteCapability(capabilityId),
                [_] when capabilityId is ProjectCapabilityIds.Run or ProjectCapabilityIds.Debug
                    => GetSelectedSolutionExplorer()?.CanExecuteStartupProject(capabilityId) == true,
                _ => false,
            };
            e.Handled = true;
        }

        private void ExecuteProjectCapability(object sender, ExecutedRoutedEventArgs e)
        {
            string? capabilityId = SolutionProjectCommands.GetCapabilityId(e.Command);
            if (capabilityId != null)
            {
                if (_selectionService.CommandNodes is [ProjectNode projectNode])
                    projectNode.ExecuteCapability(capabilityId);
                else if (_selectionService.CommandNodes.Count == 1
                    && capabilityId is ProjectCapabilityIds.Run or ProjectCapabilityIds.Debug)
                {
                    GetSelectedSolutionExplorer()?.ExecuteStartupProject(capabilityId);
                }
            }
            e.Handled = true;
        }

        private void CanExecuteSetStartupProject(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _selectionService.CommandNodes is [ProjectNode projectNode]
                && projectNode.SolutionExplorer?.CanSetStartupProject(projectNode.Project) == true;
            e.Handled = true;
        }

        private void ExecuteSetStartupProject(object sender, ExecutedRoutedEventArgs e)
        {
            if (_selectionService.CommandNodes is [ProjectNode projectNode])
                projectNode.SolutionExplorer?.SetStartupProject(projectNode.Project);
            e.Handled = true;
        }

        private void CanExecuteBuildSolution(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _selectionService.CommandNodes is [SolutionExplorer solutionExplorer]
                && solutionExplorer.CanBuildSolution();
            e.Handled = true;
        }

        private void ExecuteBuildSolution(object sender, ExecutedRoutedEventArgs e)
        {
            if (_selectionService.CommandNodes is [SolutionExplorer solutionExplorer])
                solutionExplorer.BuildSolution();
            e.Handled = true;
        }

        private void CanExecuteProjectItemMembership(object sender, CanExecuteRoutedEventArgs e)
        {
            bool include = e.Command == SolutionProjectCommands.IncludeInProject;
            e.CanExecute = TryGetProjectItemSelection(include, out _, out _);
            e.Handled = true;
        }

        private void ExecuteProjectItemMembership(object sender, ExecutedRoutedEventArgs e)
        {
            bool include = e.Command == SolutionProjectCommands.IncludeInProject;
            if (!TryGetProjectItemSelection(include, out ProjectNode? projectNode, out IReadOnlyList<SolutionNode> nodes)
                || projectNode == null)
            {
                e.Handled = true;
                return;
            }

            IReadOnlyList<SolutionNode> topLevelNodes = _selectionService.GetTopLevelNodes(nodes.Contains);
            if (!ProjectProviderRegistry.TrySetProjectItemMembership(
                projectNode.Project,
                topLevelNodes.Select(node => node.FullPath).ToList(),
                include,
                out ProjectDefinition? updatedProject,
                out string errorMessage)
                || updatedProject == null)
            {
                MessageBox.Show(
                    Application.Current?.GetActiveWindow(),
                    errorMessage,
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                e.Handled = true;
                return;
            }

            _selectionService.Clear();
            projectNode.SolutionExplorer?.ApplyProjectMutation(updatedProject);
            e.Handled = true;
        }

        private bool TryGetProjectItemSelection(
            bool include,
            out ProjectNode? projectNode,
            out IReadOnlyList<SolutionNode> nodes)
        {
            nodes = _selectionService.CommandNodes;
            projectNode = null;
            if (nodes.Count == 0 || nodes.Any(node => node is ProjectNode))
                return false;

            foreach (SolutionNode node in nodes)
            {
                ProjectNode? owner = ProjectNode.FindOwningProject(node);
                if (owner == null
                    || (projectNode != null && !string.Equals(
                        projectNode.Project.ProjectFile.FullName,
                        owner.Project.ProjectFile.FullName,
                        StringComparison.OrdinalIgnoreCase))
                    || owner.IsPathIncludedByProjectRules(node.FullPath) == include
                    || !ProjectProviderRegistry.CanChangeProjectItemMembership(owner.Project, node.FullPath))
                {
                    projectNode = null;
                    return false;
                }

                projectNode ??= owner;
            }
            return projectNode != null;
        }

        private static void CopyOrMovePaths(IEnumerable<string> sourcePaths, string targetDir, bool isMove)
        {
            foreach (var sourcePath in sourcePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                if (File.Exists(sourcePath))
                {
                    string destPath = Path.Combine(targetDir, Path.GetFileName(sourcePath));
                    if (isMove)
                    {
                        if (!IsSamePath(sourcePath, destPath) && !PathExists(destPath))
                            File.Move(sourcePath, destPath);
                    }
                    else
                    {
                        File.Copy(sourcePath, GetAvailableCopyPath(destPath, isDirectory: false));
                    }
                }
                else if (Directory.Exists(sourcePath))
                {
                    string destPath = Path.Combine(targetDir, Path.GetFileName(sourcePath));
                    if (isMove)
                    {
                        if (!IsSamePath(sourcePath, destPath) && !IsSubPathOf(destPath, sourcePath) && !PathExists(destPath))
                            Directory.Move(sourcePath, destPath);
                    }
                    else
                    {
                        string copyDestination = GetAvailableCopyPath(destPath, isDirectory: true);
                        if (IsSafeDirectoryCopyDestination(sourcePath, copyDestination))
                            CopyDirectory(sourcePath, copyDestination);
                    }
                }
            }
        }

        private static void SetClipboardPaths(string[] paths, bool isCut)
        {
            Clipboard.SetDataObject(CreatePathDataObject(paths, isCut), copy: true);
        }

        private static DataObject CreatePathDataObject(string[] paths, bool isCut)
        {
            var dataObject = new DataObject();
            dataObject.SetData(ClipboardFormat, paths);
            dataObject.SetData(DataFormats.FileDrop, paths);

            var fileDropList = new StringCollection();
            fileDropList.AddRange(paths);
            dataObject.SetFileDropList(fileDropList);
            dataObject.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(isCut ? 2 : 5)));

            return dataObject;
        }

        private bool HasClipboardPaths()
        {
            return TryGetClipboardPaths(out _, out _, out _);
        }

        private bool TryGetClipboardPaths(out string[] paths, out bool isCut, out bool isInternalClipboard)
        {
            paths = Array.Empty<string>();
            isCut = false;
            isInternalClipboard = false;

            try
            {
                var dataObject = Clipboard.GetDataObject();
                if (dataObject == null)
                    return false;

                if (dataObject.GetDataPresent(ClipboardFormat))
                {
                    isInternalClipboard = true;
                    var data = dataObject.GetData(ClipboardFormat);
                    if (data is string singlePath)
                        paths = new[] { singlePath };
                    else if (data is string[] pathArray)
                        paths = pathArray;
                }
                else if (dataObject.GetDataPresent(DataFormats.FileDrop) && dataObject.GetData(DataFormats.FileDrop) is string[] fileDropPaths)
                {
                    paths = fileDropPaths;
                }
                else if (Clipboard.ContainsFileDropList())
                {
                    paths = Clipboard.GetFileDropList().Cast<string>().ToArray();
                }

                isCut = IsPreferredDropMove(dataObject) || (isInternalClipboard && _isCutOperation);
                paths = paths.Where(path => File.Exists(path) || Directory.Exists(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                return paths.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPreferredDropMove(IDataObject dataObject)
        {
            if (!dataObject.GetDataPresent("Preferred DropEffect"))
                return false;

            if (dataObject.GetData("Preferred DropEffect") is MemoryStream stream)
            {
                stream.Position = 0;
                Span<byte> bytes = stackalloc byte[4];
                if (stream.Read(bytes) == 4)
                    return (BitConverter.ToInt32(bytes) & 2) == 2;
            }

            return false;
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var files = Directory.GetFiles(sourceDir);
            var directories = Directory.GetDirectories(sourceDir);

            Directory.CreateDirectory(destinationDir);
            foreach (var file in files)
                File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)));
            foreach (var dir in directories)
                CopyDirectory(dir, Path.Combine(destinationDir, Path.GetFileName(dir)));
        }

        private static string GetAvailableCopyPath(string desiredPath, bool isDirectory)
        {
            if (!PathExists(desiredPath))
                return desiredPath;

            string? directory = Path.GetDirectoryName(desiredPath);
            if (string.IsNullOrEmpty(directory))
                return desiredPath;

            string baseName = isDirectory
                ? Path.GetFileName(desiredPath)
                : Path.GetFileNameWithoutExtension(desiredPath);
            string extension = isDirectory ? string.Empty : Path.GetExtension(desiredPath);
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = Path.GetFileName(desiredPath);
                extension = string.Empty;
            }

            for (int count = 1; ; count++)
            {
                string candidate = Path.Combine(directory, $"{baseName} - Copy ({count}){extension}");
                if (!PathExists(candidate))
                    return candidate;
            }
        }

        private static bool PathExists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        private static bool IsSamePath(string left, string right)
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSubPathOf(string candidatePath, string parentPath)
        {
            string candidate = Path.GetFullPath(candidatePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string parent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return candidate.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsSafeDirectoryCopyDestination(string sourcePath, string destinationPath)
        {
            return !IsSamePath(sourcePath, destinationPath)
                && !IsSubPathOf(destinationPath, sourcePath);
        }

        #endregion
    }

}
