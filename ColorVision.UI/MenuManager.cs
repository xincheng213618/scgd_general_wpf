using ColorVision.Common.MVVM;
using log4net;
using System.Reflection;
using System.Windows.Controls;

namespace ColorVision.UI
{
    public interface IMenuItem
    {
        public string? OwnerGuid { get; }
        public string? GuidId { get;}
        public int Index { get; }
        public string? Header { get; }
        public string? InputGestureText { get; }
        public object? Icon { get; }
        public RelayCommand Command { get; }
    }


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

        public void LoadMenuItemFromAssembly<T>(Assembly assembly) where T : IMenuItem
        {
            var menuItems = new Dictionary<string, MenuItem>();
            menuItems.Add("File", GetFileMenuItem());
            menuItems.Add("Template", GetTemplateMenuItem());
            menuItems.Add("Tool", GetMenuToolItem());
            menuItems.Add("Help", GetMenuHelp());

            foreach (Type type in assembly.GetTypes().Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract))
            {
                if (Activator.CreateInstance(type) is T iMenuItem)
                {
                    string GuidId = iMenuItem.GuidId ?? Guid.NewGuid().ToString();
                    MenuItem menuItem = new MenuItem
                    {
                        Header = iMenuItem.Header,
                        Icon = iMenuItem.Icon,
                        InputGestureText = iMenuItem.InputGestureText,
                        Command = iMenuItem.Command,
                        Tag = iMenuItem,
                    };

                    menuItems.Add(GuidId, menuItem);
                }
            }

            foreach (var menuItem in menuItems.Values)
            {
                if (menuItem.Tag is IMenuItem iMenuItem)
                {
                    if (string.IsNullOrWhiteSpace(iMenuItem.OwnerGuid))
                    {
                        if (iMenuItem.Index < 0 || iMenuItem.Index > Menu.Items.Count)
                        {
                            Menu.Items.Add(menuItem);
                        }
                        else
                        {
                            Menu.Items.Insert(iMenuItem.Index, menuItem);
                        }
                    }
                    else if (menuItems.TryGetValue(iMenuItem.OwnerGuid, out MenuItem parentItem))
                    {
                        if (iMenuItem.Index < 0 || iMenuItem.Index > parentItem.Items.Count)
                        {
                            parentItem.Items.Add(menuItem);
                        }
                        else
                        {
                            parentItem.Items.Insert(iMenuItem.Index, menuItem);
                        }
                    }
                    else
                    {
                        if (iMenuItem.Index < 0 || iMenuItem.Index > Menu.Items.Count)
                        {
                            Menu.Items.Add(menuItem);
                        }
                        else
                        {
                            Menu.Items.Insert(iMenuItem.Index, menuItem);
                        }
                    }
                }
            }


        }

        public MenuItem? GetFileMenuItem()
        {
            foreach (var item in Menu.Items)
            {
                if (item is MenuItem menuItem && menuItem.Header.ToString() == Properties.Resources.MenuFile)
                {
                    return menuItem;
                }
            }
            return null;
        }

        public MenuItem? GetTemplateMenuItem()
        {
            foreach (var item in Menu.Items)
            {
                if (item is MenuItem menuItem && menuItem.Header.ToString() == Properties.Resources.MenuTemplate)
                {
                    return menuItem;
                }
            }
            return null;
        }

        public MenuItem? GetMenuHelp()
        {
            foreach (var item in Menu.Items)
            {
                if (item is MenuItem menuItem && menuItem.Header.ToString() == Properties.Resources.MenuHelp)
                {
                    return menuItem;
                }
            }
            return null;
        }

        public MenuItem? GetMenuToolItem()
        {
            foreach (var item in Menu.Items)
            {
                if (item is MenuItem menuItem && menuItem.Header.ToString() == Properties.Resources.MenuTool)
                {
                    return menuItem;
                }
            }
            return null;
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
