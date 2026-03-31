#pragma warning disable CS8602,CS8603,CS4014,CS8765
using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Common.Utilities;
using ColorVision.Solution.Editor;
using ColorVision.Solution.FolderMeta;
using ColorVision.Solution.Properties;
using ColorVision.UI.Menus;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Solution.Explorer
{
    public class FolderNode : SolutionNode, IDisposable
    {
        public IFolderMeta FolderMeta { get; set; }

        public DirectoryInfo DirectoryInfo { get => FolderMeta.DirectoryInfo; set { FolderMeta.DirectoryInfo = value; } }
        public RelayCommand OpenFileInExplorerCommand { get; set; }
        public RelayCommand AddDirCommand { get; set; }
        FileSystemWatcher FileSystemWatcher { get; set; }
        public bool HasFile { get => this.HasFile(); }
        public RelayCommand OpenInCmdCommand { get; set; }
        public RelayCommand OpenMethodCommand { get; set; }

        public FolderNode(IFolderMeta folder) : base()
        {
            FolderMeta = folder;
            FullPath = DirectoryInfo.FullName;
            Name1 = DirectoryInfo.Name;
            Initialize();
        }

        public override void Initialize()
        {
            base.Initialize();

            InitializeFileSystemWatcher();
            InitializeCommands();
            AddChildEventHandler += (s, e) => NotifyPropertyChanged(nameof(HasFile));

            Application.Current?.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                () => SolutionNodeFactory.PopulateChildren(this, DirectoryInfo));
        }

        private void InitializeFileSystemWatcher()
        {
            if (DirectoryInfo != null && DirectoryInfo.Exists)
            {
                FileSystemWatcher = new FileSystemWatcher(DirectoryInfo.FullName);

                FileSystemWatcher.Created += (s, e) =>
                {
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        // Duplicate protection
                        if (VisualChildren.Any(c => c.FullPath == e.FullPath))
                            return;

                        if (File.Exists(e.FullPath))
                        {
                            SolutionNodeFactory.AddFileNode(this, new FileInfo(e.FullPath));
                        }
                        else if (Directory.Exists(e.FullPath))
                        {
                            SolutionNodeFactory.AddFolderNode(this, new DirectoryInfo(e.FullPath));
                        }
                    });
                };
                FileSystemWatcher.Deleted += (s, e) =>
                {
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        var child = VisualChildren.FirstOrDefault(a => a.FullPath == e.FullPath);
                        if (child != null)
                        {
                            VisualChildren.Remove(child);
                        }
                    });
                };
                FileSystemWatcher.EnableRaisingEvents = true;
            }
        }

        private void InitializeCommands()
        {
            OpenFileInExplorerCommand = new RelayCommand(a => PlatformHelper.OpenFolder(DirectoryInfo.FullName), a => DirectoryInfo.Exists);
            AddDirCommand = new RelayCommand(a => SolutionNodeFactory.CreateNewFolder(this, DirectoryInfo.FullName));
            OpenInCmdCommand = new RelayCommand(a => System.Diagnostics.Process.Start("cmd.exe", $"/K cd \"{DirectoryInfo.FullName}\""), a => DirectoryInfo.Exists);
            OpenMethodCommand = new RelayCommand(a => OpenMethod());
        }

        public void OpenMethod()
        {
            var types = EditorManager.Instance.GetFolderEditors();
            var current = EditorManager.Instance.GetDefaultFolderEditorType();

            if (types.Count == 0) return;

            var window = new FolderEditorSelectionWindow(types, current, FullPath) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterScreen };
            if (window.ShowDialog() == true)
            {
                var selectedType = window.SelectedEditorType;
                EditorManager.Instance.SetDefaultFolderEditor(selectedType);
            }
        }

        public override void Open()
        {
            var editor = EditorManager.Instance.OpenFolder(FullPath);
            editor?.Open(FullPath);
        }

        public override void InitContextMenu()
        {
            base.InitContextMenu();
        }

        public override void InitMenuItem()
        {
            base.InitMenuItem();
            MenuItemMetadatas.AddRange(FolderMeta.GetMenuItems());
            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                GuidId = "Open",
                Order = 1,
                Header = Resources.MenuOpen,
                Command = OpenCommand
            });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "OpenMethod", Order = 2, Command = OpenMethodCommand, Header = "打开方式(_N)" });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Add", Order = 10, Header = Resources.MenuAdd });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Add", GuidId = "AddFolder", Order = 1, Header = "添加文件夹", Command = AddDirCommand });

            // Add template-based new file items under "Add" menu
            var templatesByCategory = NewItemTemplateRegistry.GetTemplatesByCategory();
            int templateOrder = 10;
            foreach (var category in templatesByCategory)
            {
                string categoryGuid = $"AddCategory_{category.Key}";
                MenuItemMetadatas.Add(new MenuItemMetadata()
                {
                    OwnerGuid = "Add",
                    GuidId = categoryGuid,
                    Order = templateOrder++,
                    Header = category.Key
                });
                int itemOrder = 1;
                foreach (var template in category.Value)
                {
                    var t = template; // capture
                    MenuItemMetadatas.Add(new MenuItemMetadata()
                    {
                        OwnerGuid = categoryGuid,
                        GuidId = $"Template_{t.Name}_{t.Extension}",
                        Order = itemOrder++,
                        Header = t.Name,
                        Icon = t.Icon,
                        Command = new RelayCommand(_ => CreateFromTemplate(t))
                    });
                }
            }

            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "MenuOpenFileInExplorer", Order = 200, Command = OpenFileInExplorerCommand, Header = Resources.MenuOpenFileInExplorer });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "OpenInCmdCommad", Order = 200, Header = "在终端中打开", Command = OpenInCmdCommand });
        }

        private void CreateFromTemplate(INewItemTemplate template)
        {
            var fileInfo = NewItemTemplateRegistry.CreateFromTemplate(template, DirectoryInfo.FullName);
            if (fileInfo != null)
            {
                var fileNode = SolutionNodeFactory.CreateFileNode(fileInfo);
                AddChild(fileNode);
                if (!IsExpanded) IsExpanded = true;
                fileNode.IsEditMode = true;
                fileNode.IsSelected = true;
            }
        }

        public override void ShowProperty()
        {
            FileProperties.ShowFolderProperties(DirectoryInfo.FullName);
        }

        public override ImageSource Icon { get => FolderMeta.Icon; set { FolderMeta.Icon = value; NotifyPropertyChanged(); } }

        public override bool ReName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowUserError("文件夹名称不允许为空");
                return false;
            }

            string? originalPath = null;
            DirectoryInfo? originalDirectoryInfo = null;
            bool fileSystemWatcherWasEnabled = false;

            try
            {
                if (DirectoryInfo.Parent != null)
                {
                    originalPath = DirectoryInfo.FullName;
                    originalDirectoryInfo = new DirectoryInfo(originalPath);

                    LogOperation($"开始重命名文件夹: {originalPath} -> {name}");

                    // 临时禁用文件系统监视器
                    if (FileSystemWatcher?.EnableRaisingEvents == true)
                    {
                        fileSystemWatcherWasEnabled = true;
                        FileSystemWatcher.EnableRaisingEvents = false;
                    }

                    foreach (var item in VisualChildren)
                    {
                        if (item is FolderNode folder && folder.FileSystemWatcher?.EnableRaisingEvents == true)
                        {
                            folder.FileSystemWatcher.EnableRaisingEvents = false;
                        }
                    }

                    string destinationDirectoryPath = Path.Combine(DirectoryInfo.Parent.FullName, name);

                    if (Directory.Exists(destinationDirectoryPath))
                    {
                        ShowUserError($"目标文件夹 '{name}' 已存在");
                        return false;
                    }

                    Directory.Move(DirectoryInfo.FullName, destinationDirectoryPath);
                    DirectoryInfo = new DirectoryInfo(destinationDirectoryPath);
                    FullPath = destinationDirectoryPath;

                    VisualChildren.Clear();
                    SolutionNodeFactory.PopulateChildren(this, DirectoryInfo);

                    if (FileSystemWatcher != null)
                    {
                        FileSystemWatcher.Path = DirectoryInfo.FullName;
                        if (fileSystemWatcherWasEnabled)
                        {
                            FileSystemWatcher.EnableRaisingEvents = true;
                        }
                    }

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
                return RollbackRename(originalPath, originalDirectoryInfo, fileSystemWatcherWasEnabled);
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
                return RollbackRename(originalPath, originalDirectoryInfo, fileSystemWatcherWasEnabled);
            }
            catch (Exception ex)
            {
                LogError($"重命名文件夹失败 - 未知错误: {ex.Message}", ex);
                ShowUserError($"重命名失败: {ex.Message}");
                return RollbackRename(originalPath, originalDirectoryInfo, fileSystemWatcherWasEnabled);
            }
        }

        private bool RollbackRename(string? originalPath, DirectoryInfo? originalDirectoryInfo, bool fileSystemWatcherWasEnabled)
        {
            try
            {
                if (originalDirectoryInfo != null && originalPath != null)
                {
                    LogOperation($"尝试回滚重命名操作: {originalPath}");
                    DirectoryInfo = originalDirectoryInfo;
                    FullPath = originalPath;

                    if (FileSystemWatcher != null)
                    {
                        FileSystemWatcher.Path = originalPath;
                        if (fileSystemWatcherWasEnabled)
                        {
                            FileSystemWatcher.EnableRaisingEvents = true;
                        }
                    }

                    VisualChildren.Clear();
                    SolutionNodeFactory.PopulateChildren(this, DirectoryInfo);
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
            if (MessageBox.Show(Application.Current.GetActiveWindow(), $"\"{Name}\"{Resources.FolderDeleteSign}", "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                base.Delete();
            }
        }

        public override bool CanReName { get => _CanReName; set { _CanReName = value; NotifyPropertyChanged(); } }
        private bool _CanReName = true;

        public override bool CanDelete { get => _CanDelete; set { _CanDelete = value; NotifyPropertyChanged(); } }
        private bool _CanDelete = true;

        public override bool CanAdd { get => _CanAdd; set { _CanAdd = value; NotifyPropertyChanged(); } }
        private bool _CanAdd = true;

        public override bool CanCopy { get => _CanCopy; set { _CanCopy = value; NotifyPropertyChanged(); } }
        private bool _CanCopy = true;

        public override bool CanPaste { get => _CanPaste; set { _CanPaste = value; NotifyPropertyChanged(); } }
        private bool _CanPaste = true;

        public override bool CanCut { get => _CanCut; set { _CanCut = value; NotifyPropertyChanged(); } }
        private bool _CanCut = true;

        #region IDisposable Support
        private bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    FileSystemWatcher?.Dispose();
                }
                _disposed = true;
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
