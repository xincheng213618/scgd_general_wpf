using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.UI.Menus;
using ColorVision.Solution.Properties;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Solution.V.Files
{
    public class VFile : VObject
    {
        public IFileMeta FileMeta { get; set; }
        public RelayCommand OpenContainingFolderCommand { get; set; }
        public RelayCommand CopyFullPathCommand { get; set; }

        public FileInfo FileInfo { get; set; }

        public VFile(IFileMeta fileMeta)
        {
            FileMeta = fileMeta;
            Name = fileMeta.Name;
            ToolTip = fileMeta.ToolTip;
            Icon = fileMeta.Icon;
            FileInfo = fileMeta.FileInfo;

            AttributesCommand = new RelayCommand(a => FileProperties.ShowFileProperties(FileMeta.FullName), a => true);
            OpenContainingFolderCommand = new RelayCommand(a => System.Diagnostics.Process.Start("explorer.exe", $"/select,{FileInfo.FullName}"), a=> FileInfo.Exists);
            CopyFullPathCommand = new RelayCommand(a => Clipboard.SetText(FileInfo.FullName), a => FileInfo.Exists);

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Open, Command = OpenCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.MenuCut, Command = ApplicationCommands.Cut, CommandParameter = this });
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.MenuCopy, Command = ApplicationCommands.Copy,CommandParameter = this });
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Delete, Command = ApplicationCommands.Delete });
            ContextMenu.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = true));

            ContextMenu.Items.Add(new MenuItem() { Header = "ReName", Command = Commands.ReName ,CommandParameter = this });

            ContextMenu.Items.Add(new Separator());
            if (fileMeta is IContextMenuProvider menuItemProvider)
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

            ContextMenu.Items.Add(new Separator());
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.MenuCopyFullPath, Command = CopyFullPathCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.MenuOpenContainingFolder, Command = OpenContainingFolderCommand });
            ContextMenu.Items.Add(new Separator());
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Property, Command = AttributesCommand });
        }


        public override void Open()
        {
            FileMeta.Open();
        }

        public override void Delete()
        {
            File.Delete(FileInfo.FullName);
            Parent.RemoveChild(this);
        }
    }
}
