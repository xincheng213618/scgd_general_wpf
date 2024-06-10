using log4net;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
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

        public void LoadMenuItemFromAssembly()
        {
            var menuItems = new Dictionary<string, MenuItem>();
            foreach (var item in Menu.Items.OfType<MenuItem>())
            {
                if (item.Name == "MenuFile")
                    menuItems.Add("File", item);
                if (item.Name == "MenuTool")
                    menuItems.Add("Tool", item);
                if (item.Name == "MenuTemplate")
                    menuItems.Add("Template", item);
                if (item.Name == "MenuHelp")
                    menuItems.Add("Help", item);
                if (item.Name == "MenuView")
                    menuItems.Add("View", item);
            }

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
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
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
        }

        public void AddMenuItem(MenuItem menuItem, int index = -1)
        {
            if (index < 0 || index > Menu.Items.Count)
            {
                Menu.Items.Add(menuItem);
            }
            else
            {
                Menu.Items.Insert(index, menuItem);
            }
        }

        public void RemoveMenuItem(MenuItem menuItem)
        {
            Menu.Items.Remove(menuItem);
        }
    }
}
