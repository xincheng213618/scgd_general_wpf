#pragma warning disable CA1822
using log4net;


namespace ColorVision.UI.Serach
{
    public class SearchManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SearchManager));
        private static SearchManager _instance;
        private static readonly object _locker = new();
        public static SearchManager GetInstance() { lock (_locker) { return _instance ??= new SearchManager(); } }

        public List<ISearch> GetISearches()
        {
            List<ISearch> searches = new List<ISearch>();
            var config = SearchConfig.Instance;

            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(ISearch).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is ISearch iMenuItem)
                    {
                        if (!string.IsNullOrWhiteSpace(iMenuItem.Header) && config.IsIndexedTypeEnabled(iMenuItem.Type))
                        {
                            searches.Add(iMenuItem);
                        }
                    }
                }

                foreach (Type type in assembly.GetTypes().Where(t => typeof(ISearchProvider).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is  ISearchProvider itemProvider)
                    {
                        searches.AddRange(itemProvider.GetSearchItems()
                            .Where(item => !string.IsNullOrWhiteSpace(item.Header) && config.IsIndexedTypeEnabled(item.Type)));
                    }
                }
            }
            return searches;
        }
    }
}
