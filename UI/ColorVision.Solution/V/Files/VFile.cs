using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.UI.Menus;
using ColorVision.Solution.Properties;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using ColorVision.UI;
using System.Windows;
using ColorVision.Common.Utilities;

namespace ColorVision.Solution.V.Files
{
    public class VFile : VObject
    {
        public IFileMeta FileMeta { get; set; }
        public RelayCommand OpenContainingFolderCommand { get; set; }
        public RelayCommand CopyFullPathCommand { get; set; }

        public FileInfo FileInfo { get => FileMeta.FileInfo; set { FileMeta.FileInfo = value; } }


        public VFile(IFileMeta fileMeta) :base()
        {
            FileMeta = fileMeta;
            ToolTip = fileMeta.ToolTip;
            Name1 = fileMeta.Name;
            Icon = fileMeta.Icon;


            ContextMenu.Items.Add(new Separator());
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.MenuCopyFullPath, Command = CopyFullPathCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.MenuOpenContainingFolder, Command = OpenContainingFolderCommand });
            ContextMenu.Items.Add(new Separator());
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Property, Command = AttributesCommand });

            if (FileMeta is IContextMenuProvider menuItemProvider)
            {
                var iMenuItems = new List<MenuItemMetadata>();
                iMenuItems.AddRange(menuItemProvider.GetMenuItems());
                iMenuItems = iMenuItems.OrderBy(item => item.Order).ToList();

                void CreateMenu(ItemsControl parentMenuItem, string? OwnerGuid)
                {
                    var iMenuItems1 = iMenuItems.FindAll(a => a.OwnerGuid == OwnerGuid).OrderBy(a => a.Order).ToList();
                    for (int i = 0; i < iMenuItems1.Count; i++)
                    {
                        var iMenuItem = iMenuItems1[i];
                        string GuidId = iMenuItem.GuidId ?? Guid.NewGuid().ToString();
                        MenuItem menuItem;
                        if (iMenuItem is IMenuItemMeta menuItemMeta)
                        {
                            menuItem = menuItemMeta.MenuItem;
                        }
                        else
                        {
                            menuItem = new MenuItem
                            {
                                Header = iMenuItem.Header,
                                Icon = iMenuItem.Icon,
                                InputGestureText = iMenuItem.InputGestureText,
                                Command = iMenuItem.Command,
                                Tag = iMenuItem,
                                Visibility = iMenuItem.Visibility,
                            };
                        }

                        CreateMenu(menuItem, GuidId);
                        if (i > 0 && iMenuItem.Order - iMenuItems1[i - 1].Order > 4 && iMenuItem.Visibility == System.Windows.Visibility.Visible)
                        {
                            parentMenuItem.Items.Add(new Separator());
                        }
                        parentMenuItem.Items.Add(menuItem);
                    }
                    foreach (var item in iMenuItems1)
                    {
                        iMenuItems.Remove(item);
                    }
                }

                CreateMenu(ContextMenu, null);
            }
        }

        public override void InitContextMenu()
        {
            AttributesCommand = new RelayCommand(a => FileProperties.ShowFileProperties(FileInfo.FullName), a => true);
            OpenContainingFolderCommand = new RelayCommand(a => PlatformHelper.OpenFolderAndSelectFile(FileInfo.FullName), a => FileInfo.Exists);
            CopyFullPathCommand = new RelayCommand(a => Common.NativeMethods.Clipboard.SetText(FileInfo.FullName), a => FileInfo.Exists);

            base.InitContextMenu();


        }


        public override void Open()
        {
            FileMeta.Open();
        }

        public override void Delete()
        {
            File.Delete(FileInfo.FullName);
            base.Delete();
        }
        public override bool ReName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) { MessageBox.Show("路径地址不允许为空"); return false; }
            try
            {
                if (FileInfo.Directory != null)
                {

                    string destinationDirectoryPath = Path.Combine(FileInfo.Directory.FullName, name);
                    File.Move(FileInfo.FullName, destinationDirectoryPath);
                    FileInfo =  new FileInfo(destinationDirectoryPath);
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
    }
}
