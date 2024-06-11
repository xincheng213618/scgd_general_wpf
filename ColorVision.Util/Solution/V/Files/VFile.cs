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

namespace ColorVision.Solution.V.Files
{
    public class VFile : VObject
    {
        public IFile FileInfos { get; set; }

        public VFile(IFile file)
        {
            FileInfos = file;
            Name = file.Name;
            ToolTip = file.ToolTip;
            Icon = file.Icon;

            AttributesCommand = new RelayCommand(a => FileProperties.ShowFileProperties(FileInfos.FullName), a => true);

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Open, Command = OpenCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = "Copy", Command = ApplicationCommands.Copy,CommandParameter = this });
            ContextMenu.Items.Add(new MenuItem() { Header = "Paste", Command = ApplicationCommands.Paste, CommandParameter = this });
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Property, Command = AttributesCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Delete, Command = DeleteCommand });
            ContextMenu.Items.Add(new Separator());
            if (file is IContextMenuProvider menuItemProvider)
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
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Property, Command = AttributesCommand });
        }

        public override void Open()
        {
            if (this is VFile vFile)
            {
                if (vFile.FileInfos is IFile file)
                {
                    file.Open();
                }
            }
        }

        public override void Copy()
        {
            if (this is VFile vFile)
            {
                if (vFile.FileInfos is IFile file)
                {
                    file.Copy();
                }
            }
        }

        public override void ReName()
        {
            if (this is VFile vFile)
            {
                if (vFile.FileInfos is IFile file)
                {
                    file.ReName();
                }
            }
        }

        public override void Delete()
        {
            if (this is VFile vFile)
            {
                if (vFile.FileInfos is IFile file)
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
