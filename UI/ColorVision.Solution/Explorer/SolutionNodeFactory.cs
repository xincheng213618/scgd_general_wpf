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

        public static void InitializeRegistries()
        {
            if (_registriesInitialized) return;
            FolderMetaRegistry.RegisterFolderMetasFromAssemblies();
            FileMetaRegistry.RegisterFileMetasFromAssemblies();
            NewItemTemplateRegistry.Initialize();
            _registriesInitialized = true;
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

        public static async Task PopulateChildren(ISolutionNode parent, DirectoryInfo directoryInfo)
        {
            foreach (var item in directoryInfo.GetDirectories())
            {
                if ((item.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                    continue;

                var folder = CreateFolderNode(item);
                parent.AddChild(folder);
            }

            int fileCount = 0;
            foreach (var item in directoryInfo.GetFiles())
            {
                fileCount++;
                if (fileCount % 10 == 0)
                {
                    await Task.Delay(100);
                }
                var sw = Stopwatch.StartNew();
                AddFileNode(parent, item);
                sw.Stop();
                log.Debug($"{item.FullName}加载时间: {sw.Elapsed.TotalSeconds} 秒");
            }
        }

        public static void AddFileNode(ISolutionNode parent, FileInfo fileInfo)
        {
            if (fileInfo.Extension.Contains("cvsln")) return;

            if (fileInfo.Extension.Contains("lnk"))
            {
                string targetPath = Common.NativeMethods.ShortcutCreator.GetShortcutTargetFile(fileInfo.FullName);
                fileInfo = new FileInfo(targetPath);
            }

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                FileNode file = CreateFileNode(fileInfo);
                parent.AddChild(file);
            });
        }

        public static async Task AddFolderNode(ISolutionNode parent, DirectoryInfo directoryInfo)
        {
            var folder = CreateFolderNode(directoryInfo);
            parent.AddChild(folder);
        }
    }
}
