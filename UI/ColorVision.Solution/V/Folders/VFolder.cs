using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Solution.Properties;
using ColorVision.UI;

namespace ColorVision.Solution.V.Folders
{
    public class VFolder : VObject
    {
        public IFolder Folder { get; set; }

        public DirectoryInfo DirectoryInfo { get => Folder.DirectoryInfo; set { Folder.DirectoryInfo = value; } }
        public RelayCommand OpenFileInExplorerCommand { get; set; }
        public RelayCommand CopyFullPathCommand { get; set; }
        public RelayCommand AddDirCommand { get; set; }
        FileSystemWatcher FileSystemWatcher { get; set; }

        public VFolder(IFolder folder) :base()
        {
            Folder = folder;
            ToolTip = folder.ToolTip;
            Name1 = folder.Name;
            FullPath = folder.DirectoryInfo.FullName;
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
            AttributesCommand = new RelayCommand(a => Common.NativeMethods.FileProperties.ShowFolderProperties(DirectoryInfo.FullName));

            ContextMenu.Items.Add(new Separator());
            MenuItem menuItem5 = new() { Header = "复制完整路径", Command = CopyFullPathCommand };
            ContextMenu.Items.Add(menuItem5);

            MenuItem menuItem3 = new() { Header = "添加" };
            MenuItem menuItem4 = new() { Header = "添加文件夹", Command = AddDirCommand };
            menuItem3.Items.Add(menuItem4);
            ContextMenu.Items.Add(menuItem3);
            ContextMenu.Items.Add(new Separator());
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Property, Command = AttributesCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.MenuOpenFileInExplorer, Command = OpenFileInExplorerCommand });
        }

        public override void InitContextMenu()
        {
            base.InitContextMenu();


        }


        public override ImageSource Icon {get => Folder.Icon; set { Folder.Icon = value; NotifyPropertyChanged(); } }

        public override void Open()
        {
            if (this is VFolder vFolder)
            {
                if (vFolder.Folder is IFolder folder)
                {
                    folder.Open();
                }
            }
        }

        public override void Copy()
        {
            if (this is VFolder vFolder)
            {
                if (vFolder.Folder is IFolder folder)
                {
                    folder.Copy();
                }
            }
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
                Folder.Delete();
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
