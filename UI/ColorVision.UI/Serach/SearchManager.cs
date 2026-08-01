#pragma warning disable CA1822
using log4net;


namespace ColorVision.UI.Serach
{
    public class SearchManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SearchManager));
        private static SearchManager _instance;
        private static readonly object _locker = new();
        private readonly object _dynamicProviderLock = new();
        private List<IDynamicSearchProvider>? _dynamicProviders;
        private int _dynamicProviderAssemblyCount = -1;
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

        public List<ISearch> SearchDynamic(string query, int limit = 30)
        {
            var searches = new List<ISearch>();
            if (string.IsNullOrWhiteSpace(query) || limit <= 0)
                return searches;

            var config = SearchConfig.Instance;
            foreach (IDynamicSearchProvider provider
                     in GetDynamicProviders())
            {
                try
                {
                    searches.AddRange(provider.Search(query, limit)
                        .Where(item =>
                            !string.IsNullOrWhiteSpace(item.Header)
                            && config.IsIndexedTypeEnabled(item.Type))
                        .Take(Math.Max(0, limit - searches.Count)));
                    if (searches.Count >= limit)
                        return searches;
                }
                catch (Exception ex)
                {
                    log.Error(
                        $"动态搜索提供器 {provider.GetType().FullName} 执行失败。",
                        ex);
                }
            }
            return searches;
        }

        private List<IDynamicSearchProvider>
            GetDynamicProviders()
        {
            var assemblies = AssemblyHandler.GetInstance()
                .GetAssemblies()
                .ToArray();
            lock (_dynamicProviderLock)
            {
                if (_dynamicProviders != null
                    && _dynamicProviderAssemblyCount
                        == assemblies.Length)
                {
                    return _dynamicProviders;
                }

                var providers = new List<IDynamicSearchProvider>();
                foreach (var assembly in assemblies)
                {
                    foreach (Type type in assembly.GetTypes().Where(
                        type => typeof(IDynamicSearchProvider)
                            .IsAssignableFrom(type)
                            && !type.IsAbstract
                            && !type.IsInterface))
                    {
                        if (Activator.CreateInstance(type)
                            is IDynamicSearchProvider provider)
                        {
                            providers.Add(provider);
                        }
                    }
                }
                _dynamicProviders = providers;
                _dynamicProviderAssemblyCount =
                    assemblies.Length;
                return providers;
            }
        }
    }
}
