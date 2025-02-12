using ColorVision.Common.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ColorVision.Common.MVVM;
using System.Runtime.Serialization;
using System.IO;
using ColorVision.Solution.Properties;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Solution.V
{

    [DataContract]
    public class VObject : INotifyPropertyChanged
    {
        public VObject Parent { get; set; }

        public virtual ObservableCollection<VObject> VisualChildren { get; set; }

        public event EventHandler AddChildEventHandler;

        public virtual void AddChild(VObject vObject)
        {
            if (vObject == null) return;
            vObject.Parent = this;
            AddChildEventHandler?.Invoke(this, new EventArgs());
            VisualChildren.SortedAdd(vObject);
        }
        public event EventHandler RemoveChildEventHandler;
        public virtual void RemoveChild(VObject vObject)
        {
            this.VisualChildren.Remove(vObject);
            RemoveChildEventHandler?.Invoke(this, new EventArgs());
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public virtual string Name { get => Name1; set
            { 
                if (Name1 == value) return; 
                if (!IsEditMode || ReName(value))
                {
                    Name1 = value;
                }
                NotifyPropertyChanged();  
            } 
        }
        protected string Name1 { get; set; } = string.Empty;

        public virtual string FullPath { get => _FullPath; set { _FullPath = value; NotifyPropertyChanged(); } }
        private string _FullPath = string.Empty;

        public virtual bool IsEditMode
        {
            get  => _IsEditMode;
            set {_IsEditMode = value; NotifyPropertyChanged(); }
        }
        private bool _IsEditMode ;

        public virtual string ToolTip { get => _ToolTip; set { _ToolTip = value; NotifyPropertyChanged(); } }
        private string _ToolTip = string.Empty;

        public virtual ImageSource? Icon { get; set; }

        public RelayCommand AddChildrenCommand { get; set; }
        public RelayCommand RemoveChildrenCommand { get; set; }
        public RelayCommand OpenCommand { get; set; }
        public RelayCommand DeleteCommand { get; set; }

        public RelayCommand PropertyCommand { get; set; }

        public virtual bool IsExpanded { get => _IsExpanded; set { _IsExpanded = value; NotifyPropertyChanged(); } }
        private bool _IsExpanded;

        public virtual bool DisableExpanded { get => _DisableExpanded; set { _DisableExpanded = value; NotifyPropertyChanged(); } }
        private bool _DisableExpanded;

        public virtual bool IsSelected { get => _IsSelected; set { _IsSelected = value; NotifyPropertyChanged(); } }
        private bool _IsSelected;

        public ContextMenu ContextMenu { get; set; }

        public List<MenuItemMetadata> MenuItemMetadatas { get; set; }

        public VObject()
        {
            VisualChildren = new ObservableCollection<VObject>() { };
            OpenCommand = new RelayCommand((s) => Open());
            MenuItemMetadatas = new List<MenuItemMetadata>();
            DeleteCommand = new RelayCommand(s =>Delete());
            PropertyCommand = new RelayCommand(s => ShowProperty());
            ContextMenu = new ContextMenu();
            ContextMenu.Initialized += (s, e) => { InitMenuItem(); InitContextMenu(); };
        }

        public virtual void InitContextMenu()
        {
            ContextMenu.Items.Clear();
            var iMenuItems = MenuItemMetadatas.OrderBy(item => item.Order).ToList();

            void CreateMenu(MenuItem parentMenuItem, string OwnerGuid)
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
                    if (i > 0 && iMenuItem.Order - iMenuItems1[i - 1].Order > 4 && iMenuItem.Visibility == Visibility.Visible)
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

            var iMenuItemMetas = MenuItemMetadatas.Where(item=>item.OwnerGuid ==MenuItemConstants.Menu && item.Visibility==Visibility.Visible).OrderBy(item => item.Order).ToList();

            for (int i = 0; i < iMenuItemMetas.Count; i++)
            {
                MenuItemMetadata  menuItemMeta = iMenuItemMetas[i];
                if (menuItemMeta.Icon is Viewbox viewbox)
                {
                    Viewbox viewbox1 = new Viewbox();

                }
                MenuItem menuItem = new MenuItem() 
                { 
                    Header = menuItemMeta.Header, 
                    Command = menuItemMeta.Command ,
                    Icon  = menuItemMeta.Icon
                };
                if (menuItemMeta.GuidId != null)
                    CreateMenu(menuItem, menuItemMeta.GuidId);
                if (i > 0 && menuItemMeta.Order - iMenuItemMetas[i - 1].Order > 4)
                    ContextMenu.Items.Add(new Separator());

                ContextMenu.Items.Add(menuItem);
            }
        }
        public virtual void InitMenuItem()
        {
            MenuItemMetadatas.Clear();
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Cut", Order = 100, Command = ApplicationCommands.Cut, Header = UI.Properties.Resources.MenuCut ,Icon = MenuItemIcon.TryFindResource("DICut") ,InputGestureText = "Crtl+X" });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Copy", Order = 101, Command = ApplicationCommands.Copy, Header = UI.Properties.Resources.MenuCopy, Icon = MenuItemIcon.TryFindResource("DICopy"), InputGestureText = "Crtl+C" });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Paste", Order = 102, Command = ApplicationCommands.Paste, Header = UI.Properties.Resources.MenuPaste, Icon =MenuItemIcon.TryFindResource("DIPaste"), InputGestureText = "Crtl+V" });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Delete", Order = 103, Command = ApplicationCommands.Delete, Header = UI.Properties.Resources.MenuDelete,Icon = MenuItemIcon.TryFindResource("DIDelete"), InputGestureText = "Del" });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "ReName", Order = 104, Command = Commands.ReName, Header = UI.Properties.Resources.MenuRename ,Icon = MenuItemIcon.TryFindResource("DIRename"), InputGestureText = "F2" });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Property", Order = 9999, Command = PropertyCommand, Header = ColorVision.Solution.Properties.Resources.MenuProperty, Icon = MenuItemIcon.TryFindResource("DIProperty") });

        }

        public virtual void ShowProperty()
        {

        }


        public virtual void Delete()
        {
            if (Parent == null)
                return;
            Parent.RemoveChild(this);
        }

        public virtual bool CanReName { get; set; } = true;
        public virtual bool CanDelete { get; set; } = true;
        public virtual bool CanAdd { get; set; } = true;
        public virtual bool CanCopy { get; set; } = true;
        public virtual bool CanPaste { get; set; } = true;
        public virtual bool CanCut { get; set; } = true;


        public void MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Open();
        }

        public virtual void Open()
        {
        }

        public virtual void Copy()
        {
            throw new NotImplementedException();
        }

        public virtual bool ReName(string name)
        {
            throw new NotImplementedException();
        }

        public virtual int CompareTo(object obj)
        {
            if (obj == null) return -1;
            else if (obj == this) return 0;
            else if (obj is VObject vObject) return Common.NativeMethods.Shlwapi.CompareLogical(Name, vObject.Name);
            else return -1;
        }
    }
}
