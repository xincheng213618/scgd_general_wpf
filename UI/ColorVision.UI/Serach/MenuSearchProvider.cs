using ColorVision.UI.Menus;

namespace ColorVision.UI.Serach
{
    public class MenuSearchProvider: ISearchProvider
    {
        public MenuSearchProvider() { }

        public IEnumerable<ISearch> GetSearchItems()
        {
            foreach (var item in MenuManager.GetInstance().GetAllMenuItemsFiltered())
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
