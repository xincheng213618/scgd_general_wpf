#pragma warning disable CA1805,CS4014,CS8602,CS8603,CS8765
using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Solution.Editor;
using ColorVision.Solution.Properties;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using System.IO;
using System.Windows;

namespace ColorVision.Solution.Explorer
{
    public class FolderNode : SolutionNode, ISolutionContainerNode, ISolutionPhysicalContainer, IDisposable
    {
        internal override string? PhysicalDeletePath => DirectoryInfo.FullName;
        public override bool CanOpen => DirectoryInfo.Exists;
        public override bool CanRefresh => DirectoryInfo.Exists;
        public override bool CanShowProperties => DirectoryInfo.Exists;
        public override string? EditorResourcePath => DirectoryInfo.FullName;
        public override string? ClipboardResourcePath => DirectoryInfo.Exists
            ? DirectoryInfo.FullName
            : null;
        public override string? ExplorerResourcePath => DirectoryInfo.Exists
            ? DirectoryInfo.FullName
            : null;
        public override string? TerminalWorkingDirectory => DirectoryInfo.Exists
            ? DirectoryInfo.FullName
            : null;
        public string PhysicalContainerPath => DirectoryInfo.FullName;
        public virtual SolutionContainerAction SupportedContainerActions => DirectoryInfo.Exists
            ? SolutionContainerAction.AddNewItem
                | SolutionContainerAction.AddExistingItem
                | SolutionContainerAction.CreateFolder
            : SolutionContainerAction.None;

        public DirectoryInfo DirectoryInfo { get; set; }
        public bool HasFile { get => this.HasFile(); }
        public RelayCommand AskCopilotSummarizeFolderCommand { get; set; }
        public RelayCommand OpenFusionCommand { get; set; }
        private bool _childrenLoaded;
        private bool _childrenLoading;
        private bool _isExpanded;
        private readonly object _childrenLoadLock = new();
        private int _childrenLoadGeneration;
        private CancellationTokenSource? _childrenLoadCancellation;
        private Task _childrenLoadTask = Task.CompletedTask;

        public bool AreChildrenLoaded => _childrenLoaded;
        internal bool AreChildrenLoading => _childrenLoading;

        public FolderNode(DirectoryInfo directoryInfo)
        {
            ArgumentNullException.ThrowIfNull(directoryInfo);
            DirectoryInfo = directoryInfo;
            FullPath = DirectoryInfo.FullName;
            Name1 = DirectoryInfo.Name;
            Icon = FileIcon.GetDirectoryIconImageSource();
            InitializeCommands();
            AddChildEventHandler += (s, e) => NotifyPropertyChanged(nameof(HasFile));
            AddLazyPlaceholderIfNeeded();
        }

        public override bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                    return;

                _isExpanded = value;
                NotifyPropertyChanged();
                if (value)
                    _ = EnsureChildrenLoadedAsync();
            }
        }

        public void EnsureChildrenLoaded()
        {
            _ = EnsureChildrenLoadedAsync();
        }

        public Task EnsureChildrenLoadedAsync()
        {
            lock (_childrenLoadLock)
            {
                if (_disposed || _childrenLoaded || !DirectoryInfo.Exists)
                    return Task.CompletedTask;
                if (_childrenLoading)
                    return _childrenLoadTask;

                int generation = ++_childrenLoadGeneration;
                var cancellation = new CancellationTokenSource();
                _childrenLoadCancellation = cancellation;
                _childrenLoading = true;
                _childrenLoadTask = LoadChildrenAsync(generation, cancellation);
                return _childrenLoadTask;
            }
        }

        private async Task LoadChildrenAsync(int generation, CancellationTokenSource cancellation)
        {
            bool completed = false;
            IReadOnlyList<SolutionNode>? loadedChildren = null;
            bool childrenAttached = false;
            try
            {
                SolutionCache? cache = SolutionNodeFactory.FindSolutionExplorer(this)?.Cache;
                IReadOnlyList<SolutionDirectoryEntrySnapshot> entries = await SolutionNodeFactory.CreateChildrenSnapshotAsync(
                    DirectoryInfo,
                    cache,
                    cancellation.Token).ConfigureAwait(false);
                IReadOnlyList<SolutionNode> materializedChildren = await SolutionNodeFactory.CreateNodesFromSnapshotAsync(
                    this,
                    entries,
                    cancellation.Token).ConfigureAwait(false);
                loadedChildren = materializedChildren;
                await InvokeOnDispatcherAsync(() =>
                {
                    lock (_childrenLoadLock)
                    {
                        if (!IsCurrentChildrenLoad(generation, cancellation))
                            return;
                        ReplaceLazyChildren(materializedChildren, loadedChildrenAreSorted: true);
                        childrenAttached = true;
                        _childrenLoaded = true;
                        completed = true;
                    }
                }, cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                LogError($"加载文件夹失败: {DirectoryInfo.FullName}", ex);
            }
            finally
            {
                if (!childrenAttached && loadedChildren != null)
                {
                    foreach (IDisposable disposable in loadedChildren.OfType<IDisposable>())
                        disposable.Dispose();
                }
                try
                {
                    await InvokeOnDispatcherAsync(() =>
                    {
                        lock (_childrenLoadLock)
                        {
                            if (!IsCurrentChildrenLoad(generation, cancellation))
                                return;
                            _childrenLoading = false;
                            if (!completed)
                                _childrenLoaded = false;
                            _childrenLoadCancellation = null;
                            _childrenLoadTask = Task.CompletedTask;
                            NotifyPropertyChanged(nameof(HasFile));
                        }
                    }, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is TaskCanceledException or InvalidOperationException)
                {
                    LogError($"完成文件夹加载状态更新失败: {DirectoryInfo.FullName}", ex);
                }
                cancellation.Dispose();
            }
        }

        private bool IsCurrentChildrenLoad(int generation, CancellationTokenSource cancellation)
        {
            return !_disposed
                && generation == _childrenLoadGeneration
                && ReferenceEquals(_childrenLoadCancellation, cancellation)
                && !cancellation.IsCancellationRequested;
        }

        private static async Task InvokeOnDispatcherAsync(Action action, CancellationToken cancellationToken)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                cancellationToken.ThrowIfCancellationRequested();
                action();
                return;
            }

            await dispatcher.InvokeAsync(
                action,
                System.Windows.Threading.DispatcherPriority.Background,
                cancellationToken).Task.ConfigureAwait(false);
        }

        public void MarkChildrenChanged()
        {
            if (_childrenLoaded)
                return;

            ResetChildrenForReload();
            if (IsExpanded)
                _ = EnsureChildrenLoadedAsync();
        }

        public void ReloadChildren()
        {
            ResetChildrenForReload();
            if (IsExpanded)
                _ = EnsureChildrenLoadedAsync();
        }

        public override void Refresh()
        {
            ReloadChildren();
        }

        private void ResetChildrenForReload()
        {
            lock (_childrenLoadLock)
            {
                _childrenLoadGeneration++;
                _childrenLoadCancellation?.Cancel();
                _childrenLoadCancellation?.Dispose();
                _childrenLoadCancellation = null;
                _childrenLoadTask = Task.CompletedTask;
                _childrenLoaded = false;
                _childrenLoading = false;
                foreach (IDisposable disposable in VisualChildren.OfType<IDisposable>().ToList())
                    disposable.Dispose();
                VisualChildren.Clear();
                AddLazyPlaceholderIfNeeded();
            }
        }

        private void AddLazyPlaceholderIfNeeded()
        {
            if (DirectoryInfo.Exists && VisualChildren.Count == 0)
                VisualChildren.Add(new LazyLoadingNode { Parent = this });
        }

        private void InitializeCommands()
        {
            AskCopilotSummarizeFolderCommand = new RelayCommand(a => AskCopilotAboutFolder(), a => DirectoryInfo.Exists);
            OpenFusionCommand = new RelayCommand(_ => OpenFusionWithFolderImages(), _ => DirectoryInfo.Exists);
        }

        public override void Open()
        {
            IsExpanded = !IsExpanded;
        }

        private void AskCopilotAboutFolder()
        {
            if (!DirectoryInfo.Exists)
                return;

            var contextItem = BuildCopilotFolderContextItem();
            CopilotLiveContextRegistry.Publish(new CopilotLiveContext
            {
                SourceId = "solution-folder-node",
                Title = contextItem.Title,
                Summary = contextItem.Summary,
                AttachmentTitle = contextItem.Title,
                SnapshotItems = new[] { contextItem },
            });

            var result = CopilotPromptRequestHelper.Dispatch(new CopilotPromptRequestOptions
                {
                    Mode = CopilotPromptMode.Code,
                    Prompt = $"请总结这个文件夹的内容结构、主要用途，以及建议优先阅读的文件。文件夹路径：{DirectoryInfo.FullName}。如有必要，请先列出目录并读取关键文本文件。",
                    ContextItems = new[] { contextItem },
                });

            if (!result.WasSent)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), result.StatusMessage, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private CopilotContextItem BuildCopilotFolderContextItem()
        {
            var directories = 0;
            var files = 0;
            try
            {
                directories = DirectoryInfo.GetDirectories().Length;
                files = DirectoryInfo.GetFiles().Length;
            }
            catch
            {
            }

            return new CopilotContextItem
            {
                Id = "solution-folder-node",
                Title = "Selected solution folder",
                Summary = DirectoryInfo.Name,
                Content = string.Join(Environment.NewLine, new[]
                {
                    $"Path: {DirectoryInfo.FullName}",
                    $"Child directories: {directories}",
                    $"Child files: {files}",
                    $"Last modified: {DirectoryInfo.LastWriteTime:O}",
                }),
            };
        }

        private void OpenFusionWithFolderImages()
        {
            var imageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };
            var imageFiles = DirectoryInfo.GetFiles()
                .Where(f => imageExts.Contains(f.Extension))
                .OrderBy(f => f.Name, Comparer<string>.Create((a, b) => Common.NativeMethods.Shlwapi.CompareLogical(a, b)))
                .Select(f => f.FullName);
            var window = new Fusion.FusionWindow(imageFiles)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Show();
        }

        private void ShowAddNewItemDialog()
        {
            var window = new AddNewItemWindow(DirectoryInfo.FullName)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (window.ShowDialog() == true && window.SelectedTemplate != null && window.NewFileName != null)
            {
                string targetPath = Path.Combine(DirectoryInfo.FullName, window.NewFileName);
                if (window.OverwriteExisting
                    && !EditorDocumentService.TryCloseDocumentsForResources([targetPath]))
                {
                    return;
                }

                SolutionPhysicalItemResult result = SolutionPhysicalItemOperations.CreateFromTemplate(
                    window.SelectedTemplate,
                    DirectoryInfo.FullName,
                    window.NewFileName,
                    window.OverwriteExisting);
                ApplyPhysicalItemResult(result, "新建项失败");
            }
        }

        private void AddExistingItem()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "添加现有项",
                Filter = "所有文件 (*.*)|*.*",
                Multiselect = true
            };
            if (dialog.ShowDialog() == true)
            {
                IReadOnlyList<string> conflicts = SolutionPhysicalItemOperations.GetImportConflictPaths(
                    dialog.FileNames,
                    DirectoryInfo.FullName);
                bool overwrite = false;
                if (conflicts.Count > 0)
                {
                    MessageBoxResult choice = MessageBox.Show(
                        Application.Current.GetActiveWindow(),
                        $"目标文件夹中已有 {conflicts.Count} 个同名文件。是否覆盖这些文件？",
                        "添加现有项",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);
                    if (choice == MessageBoxResult.Cancel)
                        return;
                    overwrite = choice == MessageBoxResult.Yes;
                    if (overwrite && !EditorDocumentService.TryCloseDocumentsForResources(conflicts))
                        return;
                }

                SolutionPhysicalItemResult result = SolutionPhysicalItemOperations.ImportFiles(
                    dialog.FileNames,
                    DirectoryInfo.FullName,
                    overwrite);
                ApplyPhysicalItemResult(result, "添加现有项失败");
            }
        }

        private void ApplyPhysicalItemResult(
            SolutionPhysicalItemResult result,
            string failureTitle)
        {
            bool projectReloaded = TryIncludeExplicitlyAddedProjectItems(
                result.SuccessfulPaths,
                out string membershipError);
            if (!projectReloaded)
                SynchronizePhysicalItems(result.SuccessfulPaths);

            if (result.Failures.Count > 0)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    SolutionPhysicalItemOperations.BuildFailureMessage(result),
                    failureTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            if (!string.IsNullOrWhiteSpace(membershipError))
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"文件已经写入磁盘，但未能包括到项目中：{membershipError}",
                    "更新项目失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private bool TryIncludeExplicitlyAddedProjectItems(
            IReadOnlyList<string> paths,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            ProjectNode? projectNode = ProjectNode.FindOwningProject(this);
            if (projectNode == null)
                return false;

            List<string> excludedPaths = paths
                .Where(path => !projectNode.IsPathIncludedByProjectRules(path))
                .ToList();
            if (excludedPaths.Count == 0)
                return false;
            if (!ProjectProviderRegistry.TrySetProjectItemMembership(
                projectNode.Project,
                excludedPaths,
                included: true,
                out ProjectDefinition? updatedProject,
                out errorMessage)
                || updatedProject == null)
            {
                return false;
            }

            if (projectNode.SolutionExplorer != null)
                projectNode.SolutionExplorer.ApplyProjectMutation(updatedProject);
            else
                projectNode.UpdateProjectDefinition(updatedProject);
            return true;
        }

        private void SynchronizePhysicalItems(IReadOnlyList<string> paths)
        {
            SolutionExplorer? explorer = SolutionNodeFactory.FindSolutionExplorer(this);
            foreach (string path in paths)
            {
                explorer?.Cache?.AddFile(path, DirectoryInfo.FullName);
                if (!VisualChildren.Any(node => string.Equals(
                    node.FullPath,
                    path,
                    StringComparison.OrdinalIgnoreCase)))
                {
                    SolutionNodeFactory.AddFileNode(this, new FileInfo(path));
                }
            }

            if (paths.Count > 0)
            {
                IsExpanded = true;
                VisualChildren.FirstOrDefault(node => paths.Contains(
                    node.FullPath,
                    StringComparer.OrdinalIgnoreCase))?.IsSelected = true;
                explorer?.NotifyVisualTreeChanged();
            }
        }

        public override void ShowProperty()
        {
            FileProperties.ShowFolderProperties(DirectoryInfo.FullName);
        }

        public override bool ReName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowUserError("文件夹名称不允许为空");
                return false;
            }

            string? originalPath = null;
            DirectoryInfo? originalDirectoryInfo = null;

            try
            {
                if (DirectoryInfo.Parent != null)
                {
                    originalPath = DirectoryInfo.FullName;
                    originalDirectoryInfo = new DirectoryInfo(originalPath);

                    LogOperation($"开始重命名文件夹: {originalPath} -> {name}");

                    string destinationDirectoryPath = Path.Combine(DirectoryInfo.Parent.FullName, name);

                    if (Directory.Exists(destinationDirectoryPath))
                    {
                        ShowUserError($"目标文件夹 '{name}' 已存在");
                        return false;
                    }

                    if (!EditorDocumentService.TryPrepareResourceRename(originalPath))
                        return false;

                    Directory.Move(DirectoryInfo.FullName, destinationDirectoryPath);
                    EditorDocumentService.NotifyResourceRenamed(originalPath, destinationDirectoryPath);
                    DirectoryInfo = new DirectoryInfo(destinationDirectoryPath);
                    FullPath = destinationDirectoryPath;

                    ResetChildrenForReload();
                    if (IsExpanded)
                        _ = EnsureChildrenLoadedAsync();

                    LogOperation($"成功重命名文件夹: {originalPath} -> {destinationDirectoryPath}");
                    return true;
                }
                else
                {
                    ShowUserError("无法重命名根目录");
                    return false;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogError($"重命名文件夹失败 - 权限不足: {ex.Message}", ex);
                ShowUserError("权限不足，无法重命名文件夹");
                return RollbackRename(originalPath, originalDirectoryInfo);
            }
            catch (DirectoryNotFoundException ex)
            {
                LogError($"重命名文件夹失败 - 目录未找到: {ex.Message}", ex);
                ShowUserError("源文件夹不存在");
                return false;
            }
            catch (IOException ex)
            {
                LogError($"重命名文件夹失败 - IO错误: {ex.Message}", ex);
                ShowUserError($"文件夹重命名失败: {ex.Message}");
                return RollbackRename(originalPath, originalDirectoryInfo);
            }
            catch (Exception ex)
            {
                LogError($"重命名文件夹失败 - 未知错误: {ex.Message}", ex);
                ShowUserError($"重命名失败: {ex.Message}");
                return RollbackRename(originalPath, originalDirectoryInfo);
            }
        }

        private bool RollbackRename(string? originalPath, DirectoryInfo? originalDirectoryInfo)
        {
            try
            {
                if (originalDirectoryInfo != null && originalPath != null)
                {
                    LogOperation($"尝试回滚重命名操作: {originalPath}");
                    DirectoryInfo = originalDirectoryInfo;
                    FullPath = originalPath;

                    ResetChildrenForReload();
                    if (IsExpanded)
                        _ = EnsureChildrenLoadedAsync();
                    LogOperation("成功回滚重命名操作");
                }
            }
            catch (Exception rollbackEx)
            {
                LogError($"回滚重命名操作失败: {rollbackEx.Message}", rollbackEx);
                ShowUserError("回滚操作也失败了，文件夹状态可能不一致");
            }

            return false;
        }

        public override void Delete()
        {
            TryDelete(showConfirmation: true);
        }

        public virtual void ExecuteContainerAction(SolutionContainerAction action)
        {
            if (!CanAdd || !this.Supports(action))
                return;

            switch (action)
            {
                case SolutionContainerAction.AddNewItem:
                    ShowAddNewItemDialog();
                    break;
                case SolutionContainerAction.AddExistingItem:
                    AddExistingItem();
                    break;
                case SolutionContainerAction.CreateFolder:
                    SolutionNodeFactory.CreateNewFolder(this, DirectoryInfo.FullName);
                    break;
            }
        }

        internal override bool TryDelete(bool showConfirmation)
        {
            if (showConfirmation
                && MessageBox.Show(Application.Current.GetActiveWindow(), $"\"{Name}\"{Resources.FolderDeleteSign}", "ColorVision", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                return false;

            if (!EditorDocumentService.TryCloseDocumentsForResources([DirectoryInfo.FullName]))
                return false;

            try
            {
                int result = ShellFileOperations.DeleteToRecycleBin(DirectoryInfo.FullName);
                if (result != 0)
                {
                    ShowUserError($"删除文件夹失败，Shell 返回代码: {result}");
                    return false;
                }
                base.TryDelete(showConfirmation: false);
                return true;
            }
            catch (Exception ex)
            {
                LogError($"删除文件夹失败: {DirectoryInfo.FullName}", ex);
                ShowUserError($"删除文件夹失败: {ex.Message}");
                return false;
            }
        }

        public override bool CanReName { get => _CanReName; set { _CanReName = value; NotifyPropertyChanged(); } }
        private bool _CanReName = true;

        public override bool CanDelete { get => _CanDelete; set { _CanDelete = value; NotifyPropertyChanged(); } }
        private bool _CanDelete = true;

        public override bool CanCopy { get => _CanCopy; set { _CanCopy = value; NotifyPropertyChanged(); } }
        private bool _CanCopy = true;

        public override bool CanCut { get => _CanCut; set { _CanCut = value; NotifyPropertyChanged(); } }
        private bool _CanCut = true;

        #region IDisposable Support
        private bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            lock (_childrenLoadLock)
            {
                if (_disposed)
                    return;
                _disposed = true;
                if (!disposing)
                    return;

                _childrenLoadGeneration++;
                _childrenLoadCancellation?.Cancel();
                _childrenLoadCancellation?.Dispose();
                _childrenLoadCancellation = null;
                foreach (var child in VisualChildren.OfType<IDisposable>().ToList())
                    child.Dispose();
                VisualChildren.Clear();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~FolderNode()
        {
            Dispose(false);
        }
        #endregion
    }
}
