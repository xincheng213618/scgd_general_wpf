using log4net;
using System.Windows.Controls;

namespace ColorVision.UI
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

        public MenuItem? FileMenuItem { get
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
