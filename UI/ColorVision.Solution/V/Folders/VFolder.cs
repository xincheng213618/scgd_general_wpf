using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Common.Utilities;
using ColorVision.Solution.Properties;
using ColorVision.UI;
using ColorVision.UI.Menus;
using Newtonsoft.Json.Linq;

namespace ColorVision.Solution.V.Folders
{
    public class VFolder : VObject
    {
        public IFolderMeta FolderMeta { get; set; }

        public DirectoryInfo DirectoryInfo { get => FolderMeta.DirectoryInfo; set { FolderMeta.DirectoryInfo = value; } }
        public RelayCommand OpenFileInExplorerCommand { get; set; }
        public RelayCommand CopyFullPathCommand { get; set; }
        public RelayCommand AddDirCommand { get; set; }
        FileSystemWatcher FileSystemWatcher { get; set; }
        public bool HasFile { get => this.HasFile(); }

        public VFolder(IFolderMeta folder) :base()
        {
            FolderMeta = folder;
            ToolTip = folder.ToolTip;
            Name1 = folder.Name;
            FullPath = DirectoryInfo.FullName;
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

            OpenFileInExplorerCommand = new RelayCommand(a => PlatformHelper.OpenFolder(DirectoryInfo.FullName), a => DirectoryInfo.Exists);
            CopyFullPathCommand = new RelayCommand(a => Common.NativeMethods.Clipboard.SetText(DirectoryInfo.FullName), a => DirectoryInfo.Exists);
            AddDirCommand = new RelayCommand(a => VMUtil.CreatFolders(this, DirectoryInfo.FullName));
            Task.Run(() => GeneralChild());
            AddChildEventHandler +=(s,e) => NotifyPropertyChanged(nameof(HasFile));
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
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Add", Order = 10, Header = ColorVision.Solution.Properties.Resources.MenuAdd });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Add", GuidId = "AddFolder", Order = 1, Header = "添加文件夹",Command = AddDirCommand });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "CopyFullPath", Order = 200, Command = CopyFullPathCommand, Header = "复制完整路径" ,Icon = MenuItemIcon.TryFindResource("DICopy") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "MenuOpenFileInExplorer", Order = 200, Command = OpenFileInExplorerCommand, Header = Resources.MenuOpenFileInExplorer });
        }

        public override void ShowProperty()
        {
            FileProperties.ShowFolderProperties(DirectoryInfo.FullName);
        }

        public override ImageSource Icon {get => FolderMeta.Icon; set { FolderMeta.Icon = value; NotifyPropertyChanged(); } }

        public override void Open()
        {

        }

        public override bool ReName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) { MessageBox.Show("路径地址不允许为空"); return false; }

            try
            {
                if (DirectoryInfo.Parent != null)
                {
                    if (FileSystemWatcher!=null)
                        FileSystemWatcher.EnableRaisingEvents = false;

                    foreach (var item in VisualChildren)
                    {
                        if (item is VFolder vFolder)
                        {
                            vFolder.FileSystemWatcher.EnableRaisingEvents = false;
                        }

                    }
                    string destinationDirectoryPath = Path.Combine(DirectoryInfo.Parent.FullName, name);
                    Directory.Move(DirectoryInfo.FullName, destinationDirectoryPath);
                    DirectoryInfo = new DirectoryInfo(destinationDirectoryPath);

                    this.VisualChildren.Clear();
                    VMUtil.Instance.GeneralChild(this,this.DirectoryInfo);
                    if (FileSystemWatcher != null)
                    {
                        FileSystemWatcher.Path = DirectoryInfo.FullName;
                        FileSystemWatcher.EnableRaisingEvents = true;
                    }
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        public override void Delete()
        {
            if (MessageBox.Show(Application.Current.GetActiveWindow(),$"\"{Name}\"{ColorVision.Solution.Properties.Resources.FolderDeleteSign}","ColorVision",MessageBoxButton.OKCancel) == MessageBoxResult.OK)
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
    }
}
