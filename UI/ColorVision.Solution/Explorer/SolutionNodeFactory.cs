using ColorVision.Solution.FileMeta;
using ColorVision.Solution.FolderMeta;
using ColorVision.Solution.Properties;
using log4net;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ColorVision.Solution.Explorer
{
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
                FolderMetaRegistry.RegisterFolderMetasFromAssemblies();
                FileMetaRegistry.RegisterFileMetasFromAssemblies();
                NewItemTemplateRegistry.Initialize();
                ProjectTemplateRegistry.Initialize();
                ProjectProviderRegistry.Initialize();
                _registriesInitialized = true;
            }
        }

        public static FolderNode CreateFolderNode(DirectoryInfo directoryInfo, SolutionExplorer? solutionExplorer = null)
        {
            if (ProjectProviderRegistry.TryLoadProject(directoryInfo, out ProjectDefinition? project)
                && project != null
                && (solutionExplorer == null || solutionExplorer.IsProjectIncluded(project)))
            {
                return new ProjectNode(CreateFolderMeta(project.ProjectDirectory), project, solutionExplorer);
            }

            return new FolderNode(CreateFolderMeta(directoryInfo));
        }

        internal static ProjectNode CreateProjectNode(ProjectDefinition project, SolutionExplorer solutionExplorer)
        {
            return new ProjectNode(CreateFolderMeta(project.ProjectDirectory), project, solutionExplorer);
        }

        private static IFolderMeta CreateFolderMeta(DirectoryInfo directoryInfo)
        {
            var folderMetaType = FolderMetaRegistry.GetFolderMetaType(directoryInfo);
            return folderMetaType != null
                ? (IFolderMeta)Activator.CreateInstance(folderMetaType, directoryInfo)!
                : new BaseFolder(directoryInfo);
        }

        public static FileNode CreateFileNode(FileInfo fileInfo)
        {
            return new FileNode(CreateFileMeta(fileInfo));
        }

        internal static IFileMeta CreateFileMeta(FileInfo fileInfo)
        {
            var extension = fileInfo.Extension;
            var fileMetaType = FileMetaRegistry.GetFileMetaTypeByExtension(extension);

            if (fileMetaType != null)
                return (IFileMeta)Activator.CreateInstance(fileMetaType, fileInfo)!;
            return new CommonFile(fileInfo);
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
            if (fileName.EndsWith(".cvsln", StringComparison.OrdinalIgnoreCase)) return true;
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
