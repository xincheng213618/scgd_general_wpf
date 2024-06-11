using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using System.Windows.Controls;
using ColorVision.Util.Properties;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms.VisualStyles;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Windows.Controls.Primitives;
using System.Diagnostics;
using ColorVision.UI.Extension;

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
            CopyFullPathCommand = new RelayCommand(a => Common.NativeMethods.Clipboard.SetText(FileInfo.FullName), a => FileInfo.Exists);

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Open, Command = OpenCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Util.Properties.Resources.MenuCut, Command = ApplicationCommands.Cut, CommandParameter = this });
            ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Util.Properties.Resources.MenuCopy, Command = ApplicationCommands.Copy,CommandParameter = this });
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Delete, Command = DeleteCommand });
            ContextMenu.Items.Add(new Separator());
            if (fileMeta is IContextMenuProvider menuItemProvider)
            {
                var iMenuItems = new List<MenuItemMetadata>();
                iMenuItems.AddRange(menuItemProvider.GetMenuItems());
                iMenuItems = iMenuItems.OrderBy(item => item.Order).ToList();

                void CreateMenu(ItemsControl parentMenuItem, string OwnerGuid)
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
            ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Util.Properties.Resources.MenuCopyFullPath, Command = CopyFullPathCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Util.Properties.Resources.MenuOpenContainingFolder, Command = OpenContainingFolderCommand });
            ContextMenu.Items.Add(new Separator());
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Property, Command = AttributesCommand });
        }


        private void OpenFolder(object parameter)
        {
            if (!string.IsNullOrEmpty(FileInfo.FullName) && File.Exists(FileInfo.FullName))
            {
                string folderPath = Path.GetDirectoryName(FileInfo.FullName);
                if (folderPath != null)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folderPath,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
            }
            else
            {
                MessageBox.Show("File path is invalid or the file does not exist.");
            }
        }

        public override void Open()
        {
            if (this is VFile vFile)
            {
                if (vFile.FileMeta is IFileMeta file)
                {
                    file.Open();
                }
            }
        }

        public override void Delete()
        {
            if (this is VFile vFile)
            {
                if (vFile.FileMeta is IFileMeta file)
                {
                    File.Delete(file.FullName);
                }
            }
            Parent.RemoveChild(this);
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
