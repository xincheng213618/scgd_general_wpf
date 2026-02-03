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

namespace ColorVision.Solution.V
{
    /// <summary>
    /// Visual representation of a folder in the solution explorer.
    /// Implements IDisposable to properly manage FileSystemWatcher resources.
    /// Provides folder-specific operations like opening in explorer, adding subfolders, etc.
    /// </summary>
    public class VFolder : VObject, IDisposable
    {
        public IFolderMeta FolderMeta { get; set; }

        public DirectoryInfo DirectoryInfo { get => FolderMeta.DirectoryInfo; set { FolderMeta.DirectoryInfo = value; } }
        public RelayCommand OpenFileInExplorerCommand { get; set; }
        public RelayCommand AddDirCommand { get; set; }
        FileSystemWatcher FileSystemWatcher { get; set; }
        public bool HasFile { get => this.HasFile(); }
        public RelayCommand OpenInCmdCommand { get; set; }
        public RelayCommand OpenMethodCommand { get; set; }

        public VFolder(IFolderMeta folder) :base()
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
            InitializeEventHandlers();
            
            Task.Run(() => GeneralChild());
        }

        private void InitializeFileSystemWatcher()
        {
            if (DirectoryInfo != null && DirectoryInfo.Exists)
            {
                FileSystemWatcher = new FileSystemWatcher(DirectoryInfo.FullName);
                
                FileSystemWatcher.Created += (s, e) =>
                {
                    if (File.Exists(e.FullPath))
                    {
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            VMUtil.Instance.CreateFile(this, new FileInfo(e.FullPath));
                        });
                        return;
                    }
                    if (Directory.Exists(e.FullPath))
                    {
                        Application.Current?.Dispatcher.Invoke(async () =>
                        {
                            await VMUtil.Instance.CreateDir(this, new DirectoryInfo(e.FullPath));
                        }); ;
                        return;
                    }
                };
                FileSystemWatcher.Deleted += (s, e) =>
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        var a = VisualChildren.FirstOrDefault(a => a.FullPath == e.FullPath);
                        if (a != null)
                        {
                            VisualChildren.Remove(a);
                        }
                    });
                };
                FileSystemWatcher.Changed += (s, e) =>
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                    });
                };
                FileSystemWatcher.Renamed += (s, e) =>
                {

                };
                FileSystemWatcher.EnableRaisingEvents = true;
            }
        }

        private void InitializeCommands()
        {
            OpenFileInExplorerCommand = new RelayCommand(a => PlatformHelper.OpenFolder(DirectoryInfo.FullName), a => DirectoryInfo.Exists);
            AddDirCommand = new RelayCommand(a => VMUtil.CreatFolders(this, DirectoryInfo.FullName));
            OpenInCmdCommand = new RelayCommand(a => System.Diagnostics.Process.Start("cmd.exe", $"/K cd \"{DirectoryInfo.FullName}\""), a => DirectoryInfo.Exists);
            OpenMethodCommand = new RelayCommand(a => OpenMethod());
        }

        /// <summary>
        /// 打开文件夹编辑器选择窗口
        /// </summary>
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

        private void InitializeEventHandlers()
        {
            AddChildEventHandler +=(s,e) => NotifyPropertyChanged(nameof(HasFile));
        }
        public override void Open()
        {
            var IEditor = EditorManager.Instance.OpenFolder(FullPath);
            IEditor?.Open(FullPath);
        }
        public virtual void GeneralChild()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                VMUtil.Instance.GeneralChild(this, DirectoryInfo);
            });
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
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Add", GuidId = "AddFolder", Order = 1, Header = "添加文件夹",Command = AddDirCommand });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "MenuOpenFileInExplorer", Order = 200, Command = OpenFileInExplorerCommand, Header = Resources.MenuOpenFileInExplorer });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "OpenInCmdCommad", Order = 200, Header = "在终端中打开", Command = OpenInCmdCommand });
        }

        public override void ShowProperty()
        {
            FileProperties.ShowFolderProperties(DirectoryInfo.FullName);
        }

        public override ImageSource Icon {get => FolderMeta.Icon; set { FolderMeta.Icon = value; NotifyPropertyChanged(); } }

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
                    
                    // 记录操作日志
                    LogOperation($"开始重命名文件夹: {originalPath} -> {name}");

                    // 临时禁用文件系统监视器
                    if (FileSystemWatcher?.EnableRaisingEvents == true)
                    {
                        fileSystemWatcherWasEnabled = true;
                        FileSystemWatcher.EnableRaisingEvents = false;
                    }

                    foreach (var item in VisualChildren)
                    {
                        if (item is VFolder vFolder && vFolder.FileSystemWatcher?.EnableRaisingEvents == true)
                        {
                            vFolder.FileSystemWatcher.EnableRaisingEvents = false;
                        }
                    }

                    string destinationDirectoryPath = Path.Combine(DirectoryInfo.Parent.FullName, name);
                    
                    // 检查目标路径是否已存在
                    if (Directory.Exists(destinationDirectoryPath))
                    {
                        ShowUserError($"目标文件夹 '{name}' 已存在");
                        return false;
                    }

                    Directory.Move(DirectoryInfo.FullName, destinationDirectoryPath);
                    DirectoryInfo = new DirectoryInfo(destinationDirectoryPath);
                    FullPath = destinationDirectoryPath;

                    VisualChildren.Clear();
                    VMUtil.Instance.GeneralChild(this, DirectoryInfo);
                    
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
                    VMUtil.Instance.GeneralChild(this, DirectoryInfo);
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
            if (MessageBox.Show(Application.Current.GetActiveWindow(),$"\"{Name}\"{Resources.FolderDeleteSign}","ColorVision",MessageBoxButton.OKCancel) == MessageBoxResult.OK)
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

        ~VFolder()
        {
            Dispose(false);
        }
        #endregion
    }
}
