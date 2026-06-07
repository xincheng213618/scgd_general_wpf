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
                _registriesInitialized = true;
            }
        }

        public static FolderNode CreateFolderNode(DirectoryInfo directoryInfo)
        {
            var folderMetaType = FolderMetaRegistry.GetFolderMetaType(directoryInfo);

            IFolderMeta folderMeta;
            if (folderMetaType != null)
            {
                folderMeta = (IFolderMeta)Activator.CreateInstance(folderMetaType, directoryInfo)!;
            }
            else
            {
                folderMeta = new BaseFolder(directoryInfo);
            }

            return new FolderNode(folderMeta);
        }

        public static FileNode CreateFileNode(FileInfo fileInfo)
        {
            var extension = fileInfo.Extension;
            var fileMetaType = FileMetaRegistry.GetFileMetaTypeByExtension(extension);

            IFileMeta fileMeta;
            if (fileMetaType != null)
            {
                fileMeta = (IFileMeta)Activator.CreateInstance(fileMetaType, fileInfo)!;
            }
            else
            {
                fileMeta = new CommonFile(fileInfo);
            }

            return new FileNode(fileMeta);
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

                FolderNode folder = CreateFolderNode(directoryInfo);
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

            if (cache != null && cache.HasCache() && cache.ValidateDirectory(directoryInfo.FullName) && TryPopulateChildrenFromCache(parent, directoryInfo, cache))
                return;

            int directoryCount = 0;
            foreach (var item in directoryInfo.GetDirectories())
            {
                directoryCount++;
                if (directoryCount % 50 == 0)
                    await Task.Yield();

                if ((item.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                    continue;

                var folder = CreateFolderNode(item);
                parent.AddChild(folder);
            }

            int fileCount = 0;
            foreach (var item in directoryInfo.GetFiles())
            {
                fileCount++;
                if (fileCount % 50 == 0)
                    await Task.Yield();

                var sw = Stopwatch.StartNew();
                AddFileNode(parent, item);
                sw.Stop();
                log.Debug($"{item.FullName}加载时间: {sw.Elapsed.TotalSeconds} 秒");
            }

        }

        private static bool TryPopulateChildrenFromCache(ISolutionNode parent, DirectoryInfo directoryInfo, SolutionCache cache)
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

                        parent.AddChild(CreateFolderNode(new DirectoryInfo(entry.FullPath)));
                    }
                    else
                    {
                        if (!File.Exists(entry.FullPath))
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
            if (fileName.EndsWith(".cvproj", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.EndsWith(".cvsln.cache.db", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static void AddFileNode(ISolutionNode parent, FileInfo fileInfo)
        {
            if (IsInternalFile(fileInfo.Name)) return;

            if (fileInfo.Extension.Contains("lnk"))
            {
                string targetPath = Common.NativeMethods.ShortcutCreator.GetShortcutTargetFile(fileInfo.FullName);
                fileInfo = new FileInfo(targetPath);
            }

            AddNode(parent, CreateFileNode(fileInfo));
        }

        public static void AddFolderNode(ISolutionNode parent, DirectoryInfo directoryInfo)
        {
            AddNode(parent, CreateFolderNode(directoryInfo));
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
