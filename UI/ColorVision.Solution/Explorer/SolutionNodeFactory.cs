using ColorVision.Solution.Properties;
using log4net;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ColorVision.Solution.Explorer
{
    internal sealed record SolutionDirectoryEntrySnapshot(string FullPath, bool IsDirectory);

    public static class SolutionNodeFactory
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SolutionNodeFactory));
        private static bool _registriesInitialized;
        private static readonly object _registryLock = new();

        public static void InitializeRegistries()
        {
            if (_registriesInitialized) return;
            lock (_registryLock)
            {
                if (_registriesInitialized) return;
                NewItemTemplateRegistry.Initialize();
                ProjectTemplateRegistry.Initialize();
                ProjectProviderRegistry.Initialize();
                SolutionMenuContributionRegistry.Initialize();
                _registriesInitialized = true;
            }
        }

        public static FolderNode CreateFolderNode(DirectoryInfo directoryInfo, SolutionExplorer? solutionExplorer = null)
        {
            if (ProjectProviderRegistry.TryLoadProject(directoryInfo, out ProjectDefinition? project)
                && project != null
                && (solutionExplorer == null || solutionExplorer.IsProjectIncluded(project)))
            {
                return new ProjectNode(project, solutionExplorer);
            }

            return new FolderNode(directoryInfo);
        }

        internal static ProjectNode CreateProjectNode(ProjectDefinition project, SolutionExplorer solutionExplorer)
        {
            return new ProjectNode(project, solutionExplorer);
        }

        public static FileNode CreateFileNode(FileInfo fileInfo)
        {
            return new FileNode(fileInfo);
        }

        public static void CreateNewFolder(SolutionNode parent, string parentFullName)
        {
            try
            {
                string name = Path.Combine(parentFullName, Resources.NewFolder);
                int count = 2;
                string newName = name;
                while (Directory.Exists(newName))
                {
                    newName = Path.Combine(parentFullName, Resources.NewFolder + "(" + count + ")");
                    count++;
                }
                Directory.CreateDirectory(newName);
                DirectoryInfo directoryInfo = new DirectoryInfo(newName);

                FolderNode folder = CreateFolderNode(directoryInfo, FindSolutionExplorer(parent));
                parent.AddChild(folder);
                parent.SortByName();
                if (!parent.IsExpanded)
                    parent.IsExpanded = true;
                folder.IsExpanded = true;
                folder.IsEditMode = true;
                folder.IsSelected = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public static async Task PopulateChildren(ISolutionNode parent, DirectoryInfo directoryInfo, SolutionCache? cache = null)
        {
            if (parent.VisualChildren.Count > 0)
                return;

            SolutionExplorer? solutionExplorer = FindSolutionExplorer(parent);
            if (cache != null && cache.HasCache() && cache.ValidateDirectory(directoryInfo.FullName) && TryPopulateChildrenFromCache(parent, directoryInfo, cache, solutionExplorer))
                return;

            int directoryCount = 0;
            foreach (var item in directoryInfo.GetDirectories())
            {
                directoryCount++;
                if (directoryCount % 50 == 0)
                    await Task.Yield();

                if ((item.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                    continue;

                if (!ShouldIncludeProjectPath(parent, item.FullName))
                    continue;

                if (solutionExplorer?.ShouldOmitPhysicalProjectDirectory(parent, item) == true)
                    continue;

                var folder = CreateFolderNode(item, solutionExplorer);
                parent.AddChild(folder);
            }

            int fileCount = 0;
            foreach (var item in directoryInfo.GetFiles())
            {
                fileCount++;
                if (fileCount % 50 == 0)
                    await Task.Yield();

                if (!ShouldIncludeProjectPath(parent, item.FullName))
                    continue;

                var sw = Stopwatch.StartNew();
                AddFileNode(parent, item);
                sw.Stop();
                log.Debug($"{item.FullName}加载时间: {sw.Elapsed.TotalSeconds} 秒");
            }

        }

        internal static Task<IReadOnlyList<SolutionDirectoryEntrySnapshot>> CreateChildrenSnapshotAsync(
            DirectoryInfo directoryInfo,
            SolutionCache? cache,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(directoryInfo);
            return Task.Run<IReadOnlyList<SolutionDirectoryEntrySnapshot>>(
                () => CreateChildrenSnapshot(directoryInfo, cache, cancellationToken),
                cancellationToken);
        }

        internal static async Task<IReadOnlyList<SolutionNode>> CreateNodesFromSnapshotAsync(
            SolutionNode parent,
            IReadOnlyList<SolutionDirectoryEntrySnapshot> entries,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(parent);
            ArgumentNullException.ThrowIfNull(entries);
            cancellationToken.ThrowIfCancellationRequested();

            SolutionExplorer? solutionExplorer = FindSolutionExplorer(parent);
            var children = new List<SolutionNode>(entries.Count);
            bool completed = false;
            try
            {
                const int batchSize = 128;
                for (int offset = 0; offset < entries.Count; offset += batchSize)
                {
                    int startIndex = offset;
                    int endIndex = Math.Min(offset + batchSize, entries.Count);
                    await InvokeOnDispatcherAsync(() =>
                    {
                        for (int index = startIndex; index < endIndex; index++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            SolutionDirectoryEntrySnapshot entry = entries[index];
                            try
                            {
                                if (entry.IsDirectory)
                                {
                                    if (!Directory.Exists(entry.FullPath)
                                        || !ShouldIncludeProjectPath(parent, entry.FullPath))
                                    {
                                        continue;
                                    }

                                    var directory = new DirectoryInfo(entry.FullPath);
                                    if (solutionExplorer?.ShouldOmitPhysicalProjectDirectory(parent, directory) == true)
                                        continue;
                                    children.Add(CreateFolderNode(directory, solutionExplorer));
                                    continue;
                                }

                                if (!File.Exists(entry.FullPath)
                                    || IsInternalFile(Path.GetFileName(entry.FullPath))
                                    || !ShouldIncludeProjectPath(parent, entry.FullPath))
                                {
                                    continue;
                                }

                                var file = new FileInfo(entry.FullPath);
                                if (solutionExplorer?.ShouldOmitPhysicalSolutionItem(parent, file) == true)
                                    continue;
                                if (file.Extension.Contains("lnk", StringComparison.OrdinalIgnoreCase))
                                {
                                    string targetPath = Common.NativeMethods.ShortcutCreator.GetShortcutTargetFile(file.FullName);
                                    file = new FileInfo(targetPath);
                                }
                                children.Add(CreateFileNode(file));
                            }
                            catch (Exception ex) when (ex is IOException
                                or UnauthorizedAccessException
                                or ArgumentException
                                or NotSupportedException)
                            {
                                log.Warn($"创建解决方案节点失败: {entry.FullPath}, {ex.Message}");
                            }
                        }
                    }, cancellationToken).ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();
                children.Sort((left, right) =>
                {
                    int result = left.CompareTo(right);
                    return result != 0
                        ? result
                        : StringComparer.OrdinalIgnoreCase.Compare(left.FullPath, right.FullPath);
                });
                completed = true;
                return children;
            }
            finally
            {
                if (!completed)
                {
                    foreach (IDisposable disposable in children.OfType<IDisposable>())
                        disposable.Dispose();
                }
            }
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

        private static IReadOnlyList<SolutionDirectoryEntrySnapshot> CreateChildrenSnapshot(
            DirectoryInfo directoryInfo,
            SolutionCache? cache,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!directoryInfo.Exists)
                return Array.Empty<SolutionDirectoryEntrySnapshot>();

            try
            {
                if (cache?.HasCache() == true && cache.ValidateDirectory(directoryInfo.FullName))
                {
                    List<FileTreeCacheEntry> cachedChildren = cache.GetChildren(directoryInfo.FullName);
                    if (cachedChildren.Count > 0)
                    {
                        return cachedChildren
                            .Select(entry => new SolutionDirectoryEntrySnapshot(entry.FullPath, entry.IsDirectory))
                            .ToList();
                    }
                }

                var entries = new List<SolutionDirectoryEntrySnapshot>();
                foreach (DirectoryInfo directory in directoryInfo.EnumerateDirectories())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if ((directory.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        continue;
                    entries.Add(new SolutionDirectoryEntrySnapshot(directory.FullName, IsDirectory: true));
                }

                foreach (FileInfo file in directoryInfo.EnumerateFiles())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsInternalFile(file.Name))
                        entries.Add(new SolutionDirectoryEntrySnapshot(file.FullName, IsDirectory: false));
                }
                return entries;
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
            {
                log.Warn($"枚举解决方案目录失败: {directoryInfo.FullName}, {ex.Message}");
                return Array.Empty<SolutionDirectoryEntrySnapshot>();
            }
        }

        private static bool TryPopulateChildrenFromCache(
            ISolutionNode parent,
            DirectoryInfo directoryInfo,
            SolutionCache cache,
            SolutionExplorer? solutionExplorer)
        {
            var cachedChildren = cache.GetChildren(directoryInfo.FullName);
            if (cachedChildren.Count == 0)
                return false;

            foreach (var entry in cachedChildren)
            {
                try
                {
                    if (entry.IsDirectory)
                    {
                        if (!Directory.Exists(entry.FullPath))
                            continue;
                        if (!ShouldIncludeProjectPath(parent, entry.FullPath))
                            continue;

                        var childDirectory = new DirectoryInfo(entry.FullPath);
                        if (solutionExplorer?.ShouldOmitPhysicalProjectDirectory(parent, childDirectory) == true)
                            continue;

                        parent.AddChild(CreateFolderNode(childDirectory, solutionExplorer));
                    }
                    else
                    {
                        if (!File.Exists(entry.FullPath))
                            continue;
                        if (!ShouldIncludeProjectPath(parent, entry.FullPath))
                            continue;

                        AddFileNode(parent, new FileInfo(entry.FullPath));
                    }
                }
                catch (Exception ex)
                {
                    log.Warn($"从缓存加载节点失败: {entry.FullPath}, {ex.Message}");
                }
            }

            return parent.VisualChildren.Count > 0;
        }

        public static bool HasVisibleChildren(DirectoryInfo directoryInfo)
        {
            try
            {
                foreach (var directory in directoryInfo.EnumerateDirectories())
                {
                    if ((directory.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                        return true;
                }

                foreach (var file in directoryInfo.EnumerateFiles())
                {
                    if (!IsInternalFile(file.Name))
                        return true;
                }
            }
            catch
            {
            }

            return false;
        }

        /// <summary>
        /// Check if a file is an internal solution file that should be hidden from the tree.
        /// </summary>
        public static bool IsInternalFile(string fileName)
        {
            if (SolutionManager.IsSolutionFilePath(fileName)) return true;
            if (ProjectProviderRegistry.IsSupportedProjectFilePath(fileName)) return true;
            if (fileName.EndsWith(".cvsln.cache.db", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.EndsWith(".cvsln.bak", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.Contains(".cvsln.corrupt-", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                && (fileName.Contains(".cvsln.", StringComparison.OrdinalIgnoreCase)
                    || fileName.Contains(".cvproj.", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
            return false;
        }

        public static void AddFileNode(ISolutionNode parent, FileInfo fileInfo)
        {
            if (IsInternalFile(fileInfo.Name)) return;
            if (!ShouldIncludeProjectPath(parent, fileInfo.FullName)) return;
            if (FindSolutionExplorer(parent)?.ShouldOmitPhysicalSolutionItem(parent, fileInfo) == true) return;

            if (fileInfo.Extension.Contains("lnk"))
            {
                string targetPath = Common.NativeMethods.ShortcutCreator.GetShortcutTargetFile(fileInfo.FullName);
                fileInfo = new FileInfo(targetPath);
            }

            AddNode(parent, CreateFileNode(fileInfo));
        }

        public static void AddFolderNode(ISolutionNode parent, DirectoryInfo directoryInfo)
        {
            if (!ShouldIncludeProjectPath(parent, directoryInfo.FullName))
                return;

            SolutionExplorer? solutionExplorer = FindSolutionExplorer(parent);
            if (solutionExplorer?.ShouldOmitPhysicalProjectDirectory(parent, directoryInfo) == true)
                return;
            AddNode(parent, CreateFolderNode(directoryInfo, solutionExplorer));
        }

        internal static SolutionExplorer? FindSolutionExplorer(ISolutionNode parent)
        {
            if (parent is SolutionExplorer solutionExplorer)
                return solutionExplorer;

            SolutionNode? node = parent as SolutionNode;
            while (node != null)
            {
                if (node is SolutionExplorer owner)
                    return owner;
                node = node.Parent;
            }
            return null;
        }

        private static bool ShouldIncludeProjectPath(ISolutionNode parent, string fullPath)
        {
            SolutionNode? node = parent as SolutionNode;
            while (node != null)
            {
                if (node is ProjectNode projectNode)
                    return projectNode.IncludesPath(fullPath);
                node = node.Parent;
            }
            return true;
        }

        private static void AddNode(ISolutionNode parent, SolutionNode node)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                parent.AddChild(node);
            }
            else
            {
                dispatcher.BeginInvoke(() => parent.AddChild(node));
            }
        }
    }
}
