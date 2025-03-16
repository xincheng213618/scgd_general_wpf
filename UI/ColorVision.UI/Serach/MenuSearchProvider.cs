using ColorVision.UI.Menus;

namespace ColorVision.UI.Serach
{
    public class MenuSearchProvider: ISearchProvider
    {
        public MenuSearchProvider() { }

        public IEnumerable<ISearch> GetSearchItems()
        {
            List<IMenuItem> MenuItems = new List<IMenuItem>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IMenuItem).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IMenuItem iMenuItem)
                    {
                        MenuItems.Add(iMenuItem);
                    }
                }

                foreach (Type type in assembly.GetTypes().Where(t => typeof(IMenuItemProvider).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IMenuItemProvider itemProvider)
                    {
                        MenuItems.AddRange(itemProvider.GetMenuItems());
                    }
                }
            }

            foreach (var item in MenuItems)
            {
                if (item.Header !=null && item.Command !=null)
                {
                    SearchMeta search = new SearchMeta
                    {
                        GuidId = item.GuidId,
                        Header = item.Header,
                        Icon = item.Icon,
                        Command = item.Command,
                    };
                    yield return search;
                }

            }
        }
    }
}
