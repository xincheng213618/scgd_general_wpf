using ColorVision.Common.MVVM;
using log4net;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.UI.Menus
{
    public class MenuManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MenuManager));
        private static MenuManager _instance;
        private static readonly object _locker = new();
        public static MenuManager GetInstance() { lock (_locker) { return _instance ??= new MenuManager(); } }

        public Menu Menu { get; set; }

        public MenuManager()
        {

        }
        private bool Initialized;
        private List<MenuItem> MenuBack;
        public void LoadMenuItemFromAssembly()
        {
            if (!Initialized)
            {
                MenuBack = new List<MenuItem>();
                foreach (var item in Menu.Items.OfType<MenuItem>().ToList())
                {
                    Menu.Items.Remove(item);
                    MenuBack.Add(item);
                }
            }


            Initialized = true;
            log.Info("LoadMenuItemFromAssembly");
            Menu.Items.Clear();
            var menuItems = new Dictionary<string, MenuItem>();
            List<IMenuItem> iMenuItems = new();
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
                        if (iMenuItem.Command is RelayCommand relayCommand)
                        {
                            menuItem.Visibility = iMenuItem.Visibility == Visibility.Visible ? relayCommand.CanExecute(null) ? Visibility.Visible : Visibility.Collapsed : Visibility.Collapsed;
                            Authorizations.Authorization.Instance.PermissionModeChanged += (s, e) =>
                            {
                                menuItem.Visibility = iMenuItem.Visibility == Visibility.Visible ? relayCommand.CanExecute(null) ? Visibility.Visible : Visibility.Collapsed : Visibility.Collapsed;
                            };
                        }
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
         
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IMenuItem).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IMenuItem iMenuItem)
                    {
                        iMenuItems.Add(iMenuItem);
                    }
                }

                foreach (Type type in assembly.GetTypes().Where(t => typeof(IMenuItemProvider).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IMenuItemProvider itemProvider)
                    {
                        iMenuItems.AddRange(itemProvider.GetMenuItems());
                    }
                }
            }

            foreach (var item in iMenuItems.Where(a=>a.OwnerGuid == "Menu").OrderBy(item => item.Order).ToList())
            {
                if (item.OwnerGuid == "Menu" && item.Header !=null)
                {
                    MenuItem menuItem = new()
                    {
                        Header = item.Header,
                        Icon = item.Icon,
                        InputGestureText = item.InputGestureText,
                        Command = item.Command,
                        Tag = item,
                        Visibility = item.Visibility,
                    };
                    menuItems.Add(item.GuidId ?? Guid.NewGuid().ToString(), menuItem);  
                    Menu.Items.Add(menuItem);
                    iMenuItems.Remove(item);
                }
            }

            foreach (var keyValuePair in menuItems)
            {
                CreateMenu(keyValuePair.Value, keyValuePair.Key);
            }
            
            iMenuItems = iMenuItems.OrderBy(item => item.Order).ToList();
            foreach (var iMenuItem in iMenuItems)
            {
                string GuidId = iMenuItem.GuidId ?? Guid.NewGuid().ToString();
                MenuItem menuItem = new()
                {
                    Header = iMenuItem.Header,
                    Icon = iMenuItem.Icon,
                    InputGestureText = iMenuItem.InputGestureText,
                    Command = iMenuItem.Command,
                    Tag = iMenuItem,
                    Visibility = iMenuItem.Visibility,
                };
                Menu.Items.Add(menuItem);
            }
            var list = Menu.Items.OfType<MenuItem>().ToList();
            foreach (var item in MenuBack)
            {
                var men = list.FirstOrDefault(a => a.Header!=null && a.Header.ToString() == item.Header.ToString());
                if (men is null)
                {
                    Menu.Items.Add(item);
                }
                else
                {
                    foreach (var item1 in item.Items.OfType<MenuItem>().ToList())
                    {
                        item.Items.Remove(item1);
                        men.Items.Add(item1);
                    }
                }
            }

        }
    }
}
