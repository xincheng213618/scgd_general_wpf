using ColorVision.Common.MVVM;
using log4net;
using System.Reflection;
using System.Windows.Controls;

namespace ColorVision.UI
{
    public interface IMenuItem
    {
        int Index { get; }
        string? Header { get; }
        string? InputGestureText { get; }
        string? Icon { get; }
        RelayCommand Command { get; }
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

        public static List<T> LoadAssembly<T>(Assembly assembly) where T : IMenuItem
        {
            List<T> plugins = new List<T>();
            foreach (Type type in assembly.GetTypes())
            {
                if (type.GetInterfaces().Contains(typeof(T)))
                {
                    if (Activator.CreateInstance(type) is T plugin)
                    {
                        plugins.Add(plugin);
                    }
                }
            }
            return plugins;
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
