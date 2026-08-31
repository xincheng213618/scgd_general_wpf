using ColorVision.Common.MVVM;
using ColorVision.UI.HotKey;
using log4net;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace ColorVision.UI.Serach;

/// <summary>Coordinates UI-owned providers without moving legacy UI objects to worker threads.</summary>
public class SearchManager
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(SearchManager));
    private static readonly Lazy<SearchManager> Instance = new(() => new SearchManager());
    private const int ProviderLimit = 20;
    private const int CatalogLimit = 5000;
    private readonly Func<Assembly[]> _getAssemblies;
    private readonly Func<Assembly, IEnumerable<Type>> _getTypes;
    private readonly Func<SearchConfig> _getConfig;
    private readonly Func<IEnumerable<HotKeys>> _getHotkeys;
    private readonly Func<Type, object?> _createProvider;
    private Assembly[]? _assemblies;
    private List<(object Provider, string Id)> _providers = [];
    private List<(ISearch Source, string ProviderId)>? _staticEntries;
    private readonly List<string> _discoveryFailures = [];
    private readonly List<string> _staticFailures = [];
    private readonly List<string> _recentIds = [];
    private bool _catalogTruncated;

    public SearchManager() : this(() => AssemblyHandler.Instance.GetAssemblies(), assembly => assembly.GetTypes(),
        () => SearchConfig.Instance, () => HotkeyService.GetInstance().HotKeys) { }

    internal SearchManager(Func<Assembly[]> getAssemblies, Func<Assembly, IEnumerable<Type>> getTypes,
        Func<SearchConfig> getConfig, Func<IEnumerable<HotKeys>> getHotkeys, Func<Type, object?>? createProvider = null)
    {
        _getAssemblies = getAssemblies;
        _getTypes = getTypes;
        _getConfig = getConfig;
        _getHotkeys = getHotkeys;
        _createProvider = createProvider ?? Activator.CreateInstance;
    }

    public static SearchManager GetInstance() => Instance.Value;

    /// <summary>Refresh contributed data next time the palette opens without repeating type discovery.</summary>
    public void InvalidateCatalog() => _staticEntries = null;

    public IReadOnlyList<SearchResultItem> GetStaticResults(bool refresh = false)
    {
        EnsureCatalog(refresh);
        SearchConfig config = _getConfig();
        Dictionary<string, string> shortcuts = GetShortcuts();
        var results = new List<SearchResultItem>();
        foreach (var entry in _staticEntries!)
        {
            try
            {
                if (IsIncluded(entry.Source, config)) results.Add(CreateResult(entry.Source, entry.ProviderId, shortcuts));
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                ReportFailure(_staticFailures, entry.ProviderId, exception);
            }
        }
        return results;
    }

    /// <summary>Compatibility API; explicitly requesting the old catalog API still refreshes contributed items.</summary>
    public List<ISearch> GetISearches() => GetStaticResults(refresh: true).Select(item => item.Source).ToList();

    public async Task<SearchQueryResult> QueryAsync(string query, CancellationToken cancellationToken,
        int limit = 60, string? category = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (limit <= 0) return new([], [], false);
        limit = Math.Min(limit, 200);
        query = query.Trim();
        IReadOnlyList<SearchResultItem> staticItems = GetStaticResults();
        var failures = _discoveryFailures.Concat(_staticFailures).Distinct(StringComparer.Ordinal).ToList();
        var dynamicItems = new List<SearchResultItem>();
        bool truncated = _catalogTruncated;
        if (query.Length > 0)
        {
            SearchConfig config = _getConfig();
            Dictionary<string, string> shortcuts = GetShortcuts();
            // A snapshot allows another query to refresh discovery while an async provider awaits I/O.
            foreach (var entry in _providers.ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.Provider is not IDynamicSearchProvider && entry.Provider is not IAsyncSearchProvider) continue;
                try
                {
                    // Legacy providers may read WPF controls or business collections. Do not Task.Run them.
                    IEnumerable<ISearch> source = entry.Provider is IAsyncSearchProvider asyncProvider
                        ? await asyncProvider.SearchAsync(query, ProviderLimit + 1, cancellationToken).WaitAsync(cancellationToken)
                        : ((IDynamicSearchProvider)entry.Provider).Search(query, ProviderLimit + 1);
                    cancellationToken.ThrowIfCancellationRequested();
                    List<SearchResultItem> items = source.Where(item => IsIncluded(item, config))
                        .Take(ProviderLimit + 1).Select(item => CreateResult(item, entry.Id, shortcuts)).ToList();
                    truncated |= items.Count > ProviderLimit;
                    dynamicItems.AddRange(items);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    ReportFailure(failures, entry.Id, exception);
                }
            }
            dynamicItems.AddRange(GetExternalResults(query, config));
        }
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<SearchResultItem> itemsWithOverflow = SearchQuery.MatchAndRank(staticItems, dynamicItems, query,
            limit + 1, category, _recentIds, ProviderLimit);
        // Also account for source quotas, rather than silently claiming that the capped list is exhaustive.
        truncated |= itemsWithOverflow.Count > limit || SearchQuery.MatchAndRank(staticItems, dynamicItems, query,
            limit + 1, category, _recentIds, int.MaxValue).Count > itemsWithOverflow.Count;
        return new(itemsWithOverflow.Take(limit).ToArray(), failures, truncated);
    }

    /// <summary>Legacy synchronous providers only. New UI callers should use QueryAsync.</summary>
    public List<ISearch> SearchDynamic(string query, int limit = 30)
    {
        if (string.IsNullOrWhiteSpace(query) || limit <= 0) return [];
        EnsureCatalog(false);
        var results = new List<SearchResultItem>();
        var failures = new List<string>();
        SearchConfig config = _getConfig();
        Dictionary<string, string> shortcuts = GetShortcuts();
        foreach (var entry in _providers)
        {
            if (entry.Provider is not IDynamicSearchProvider provider) continue;
            try
            {
                results.AddRange(provider.Search(query, Math.Min(limit, ProviderLimit))
                    .Where(item => IsIncluded(item, config)).Take(ProviderLimit)
                    .Select(item => CreateResult(item, entry.Id, shortcuts)));
            }
            catch (Exception exception) when (exception is not OutOfMemoryException) { ReportFailure(failures, entry.Id, exception); }
        }
        return SearchQuery.MatchAndRank([], results, query, limit).Select(item => item.Source).ToList();
    }

    /// <summary>Session-only history; never persists query text or invokes a command.</summary>
    public void RecordUsed(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId)) return;
        _recentIds.RemoveAll(id => string.Equals(id, stableId, StringComparison.OrdinalIgnoreCase));
        _recentIds.Insert(0, stableId);
        if (_recentIds.Count > 10) _recentIds.RemoveAt(10);
    }

    private void EnsureCatalog(bool refresh)
    {
        Assembly[] assemblies = _getAssemblies().Distinct().ToArray();
        if (_assemblies == null || !_assemblies.SequenceEqual(assemblies))
        {
            _assemblies = assemblies;
            _staticEntries = null;
            _providers = [];
            _discoveryFailures.Clear();
            foreach (Assembly assembly in assemblies)
            {
                IEnumerable<Type> types;
                try { types = _getTypes(assembly).ToArray(); }
                catch (ReflectionTypeLoadException exception)
                {
                    ReportFailure(_discoveryFailures, assembly.GetName().Name ?? "Assembly", exception);
                    types = exception.Types.OfType<Type>();
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    ReportFailure(_discoveryFailures, assembly.GetName().Name ?? "Assembly", exception);
                    continue;
                }
                foreach (Type type in types.Where(IsProviderType))
                {
                    string id = type.FullName ?? type.Name;
                    try
                    {
                        object? provider = _createProvider(type);
                        if (provider != null) _providers.Add((provider, id));
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException) { ReportFailure(_discoveryFailures, id, exception); }
                }
            }
        }
        if (_staticEntries != null && !refresh) return;
        _staticEntries = [];
        _staticFailures.Clear();
        _catalogTruncated = false;
        foreach (var entry in _providers)
        {
            try
            {
                var items = new List<ISearch>();
                if (entry.Provider is ISearch item) items.Add(item);
                if (entry.Provider is ISearchProvider provider) items.AddRange(provider.GetSearchItems().Take(CatalogLimit + 1));
                _catalogTruncated |= items.Count > CatalogLimit;
                _staticEntries.AddRange(items.Take(CatalogLimit).Select(item => (item, entry.Id)));
            }
            catch (Exception exception) when (exception is not OutOfMemoryException) { ReportFailure(_staticFailures, entry.Id, exception); }
        }
    }

    private static bool IsProviderType(Type type) => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters
        && type.GetConstructor(Type.EmptyTypes) != null && (typeof(ISearch).IsAssignableFrom(type)
        || typeof(ISearchProvider).IsAssignableFrom(type) || typeof(IDynamicSearchProvider).IsAssignableFrom(type)
        || typeof(IAsyncSearchProvider).IsAssignableFrom(type));

    private static bool IsIncluded(ISearch? item, SearchConfig config)
        => item != null && !string.IsNullOrWhiteSpace(item.Header) && item.Command != null && config.IsIndexedTypeEnabled(item.Type);

    private Dictionary<string, string> GetShortcuts() => _getHotkeys().Where(item => !string.IsNullOrWhiteSpace(item.Id))
        .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => string.Join(" / ", group.First().GetBindings().Select(HotkeyInput.Format)), StringComparer.OrdinalIgnoreCase);

    private static SearchResultItem CreateResult(ISearch item, string providerId, IReadOnlyDictionary<string, string> shortcuts)
    {
        string? actionId = (item as ISearchMetadata)?.ActionId;
        return new(item, providerId, actionId != null && shortcuts.TryGetValue(actionId, out string? shortcut) ? shortcut : string.Empty);
    }

    private IEnumerable<SearchResultItem> GetExternalResults(string query, SearchConfig config)
    {
        string path = config.EverythingPath;
        if (config.EnableEverythingSearch && File.Exists(path))
        {
            yield return new(new SearchMeta
            {
                Type = SearchType.Link, GuidId = "external:everything", CategoryKey = "External",
                Header = string.Format(CultureInfo.CurrentUICulture, SearchPaletteText.Get("SearchWithFormat"), "Everything"),
                Description = query,
                Command = new RelayCommand(_ => Process.Start(CreateEverythingStartInfo(path, query)),
                    _ => _getConfig().EnableEverythingSearch && string.Equals(_getConfig().EverythingPath, path, StringComparison.OrdinalIgnoreCase) && File.Exists(path)),
            }, "External");
        }
        SearchEngine engine = config.SearchEngine;
        if (config.EnableBrowserSearch)
        {
            yield return new(new SearchMeta
            {
                Type = SearchType.Link, GuidId = "external:browser", CategoryKey = "External",
                Header = string.Format(CultureInfo.CurrentUICulture, SearchPaletteText.Get("SearchWithFormat"), engine),
                Description = query,
                Command = new RelayCommand(_ => Process.Start(new ProcessStartInfo { FileName = GetBrowserSearchUrl(query, engine), UseShellExecute = true }),
                    _ => _getConfig().EnableBrowserSearch && _getConfig().SearchEngine == engine),
            }, "External");
        }
    }

    internal static ProcessStartInfo CreateEverythingStartInfo(string path, string query)
    {
        var startInfo = new ProcessStartInfo { FileName = path, UseShellExecute = true, WorkingDirectory = Environment.CurrentDirectory };
        startInfo.ArgumentList.Add("-s");
        startInfo.ArgumentList.Add(query);
        return startInfo;
    }

    internal static string GetBrowserSearchUrl(string query, SearchEngine engine) => engine switch
    {
        SearchEngine.Baidu => "https://www.baidu.com/s?wd=" + Uri.EscapeDataString(query),
        SearchEngine.Bing => "https://www.bing.com/search?q=" + Uri.EscapeDataString(query),
        _ => "https://www.google.com/search?q=" + Uri.EscapeDataString(query),
    };

    private static void ReportFailure(List<string> failures, string source, Exception exception)
    {
        if (!failures.Contains(source, StringComparer.Ordinal)) failures.Add(source);
        Log.Warn($"Search source {source} failed.", exception);
    }
}
