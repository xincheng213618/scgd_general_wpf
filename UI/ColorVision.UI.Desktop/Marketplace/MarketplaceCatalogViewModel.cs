using ColorVision.Common.MVVM;
using ColorVision.UI.Desktop.Properties;
using ColorVision.UI.Marketplace;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Desktop.Marketplace
{
    public sealed class MarketplaceOptionItem
    {
        public string Key { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
    }

    public sealed class MarketplaceCatalogViewModel : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MarketplaceCatalogViewModel));
        private readonly MarketplaceClient _client = MarketplaceClient.GetInstance();
        private readonly Func<string, PluginInfoVM?> _installedPluginLookup;
        private readonly Action<MarketplaceDetailContext?> _detailChanged;
        private CancellationTokenSource? _loadPageCancellation;
        private CancellationTokenSource? _loadDetailCancellation;
        private MarketplacePluginSummary? _selectedPlugin;
        private MarketplaceDetailContext? _selectedDetailContext;
        private MarketplaceOptionItem _selectedCategory;
        private MarketplaceOptionItem _selectedSortOption;
        private string _keyword = string.Empty;
        private string _author = string.Empty;
        private bool _isInitialized;
        private bool _suspendAutoRefresh;
        private bool _isLoading;
        private bool _isLoadingDetail;
        private bool _hasError;
        private bool _isOffline;
        private string _statusText = string.Empty;
        private int _page = 1;
        private int _pageSize = 20;
        private int _totalCount;
        private int _totalPages;

        public MarketplaceCatalogViewModel(Func<string, PluginInfoVM?> installedPluginLookup, Action<MarketplaceDetailContext?> detailChanged)
        {
            _installedPluginLookup = installedPluginLookup;
            _detailChanged = detailChanged;

            Categories = new ObservableCollection<MarketplaceOptionItem>();
            SortOptions = new ObservableCollection<MarketplaceOptionItem>
            {
                new() { Key = "updated", DisplayName = "最近更新" },
                new() { Key = "downloads", DisplayName = "下载量" },
                new() { Key = "name", DisplayName = "名称" },
            };
            MarketplacePlugins = new ObservableCollection<MarketplacePluginSummary>();
            PackageSuggestions = new ObservableCollection<string>();
            MarketplacePlugins.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(HasItems));
                OnPropertyChanged(nameof(IsEmpty));
            };

            _selectedCategory = new MarketplaceOptionItem { Key = string.Empty, DisplayName = "全部分类" };
            _selectedSortOption = SortOptions[0];
            Categories.Add(_selectedCategory);

            RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync(forceReload: true), logger: log);
            PreviousPageCommand = new AsyncRelayCommand(_ => ChangePageAsync(Page - 1), _ => CanGoPrevious, logger: log);
            NextPageCommand = new AsyncRelayCommand(_ => ChangePageAsync(Page + 1), _ => CanGoNext, logger: log);
        }

        public ObservableCollection<MarketplaceOptionItem> Categories { get; }
        public ObservableCollection<MarketplaceOptionItem> SortOptions { get; }
        public ObservableCollection<MarketplacePluginSummary> MarketplacePlugins { get; }
        public ObservableCollection<string> PackageSuggestions { get; }

        public AsyncRelayCommand RefreshCommand { get; }
        public AsyncRelayCommand PreviousPageCommand { get; }
        public AsyncRelayCommand NextPageCommand { get; }

        public MarketplacePluginSummary? SelectedPlugin
        {
            get => _selectedPlugin;
            set
            {
                _selectedPlugin = value;
                OnPropertyChanged();
                _ = LoadSelectedDetailAsync(value);
            }
        }

        public MarketplaceDetailContext? SelectedDetailContext
        {
            get => _selectedDetailContext;
            private set
            {
                _selectedDetailContext = value;
                OnPropertyChanged();
                _detailChanged(value);
            }
        }

        public MarketplaceOptionItem SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                _selectedCategory = value ?? Categories.First();
                OnPropertyChanged();
                if (!_suspendAutoRefresh)
                {
                    QueueRefresh(resetPage: true, debounceMilliseconds: 0);
                }
            }
        }

        public MarketplaceOptionItem SelectedSortOption
        {
            get => _selectedSortOption;
            set
            {
                _selectedSortOption = value ?? SortOptions.First();
                OnPropertyChanged();
                if (!_suspendAutoRefresh)
                {
                    QueueRefresh(resetPage: true, debounceMilliseconds: 0);
                }
            }
        }

        public string Keyword
        {
            get => _keyword;
            set
            {
                _keyword = value ?? string.Empty;
                OnPropertyChanged();
                QueueRefresh(resetPage: true, debounceMilliseconds: 300);
            }
        }

        public string Author
        {
            get => _author;
            set
            {
                _author = value ?? string.Empty;
                OnPropertyChanged();
                QueueRefresh(resetPage: true, debounceMilliseconds: 300);
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasItems));
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        public bool IsLoadingDetail
        {
            get => _isLoadingDetail;
            private set
            {
                _isLoadingDetail = value;
                OnPropertyChanged();
            }
        }

        public bool HasError
        {
            get => _hasError;
            private set
            {
                _hasError = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        public bool IsOffline
        {
            get => _isOffline;
            private set
            {
                _isOffline = value;
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public int Page
        {
            get => _page;
            private set
            {
                _page = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageDisplayText));
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
                PreviousPageCommand.RaiseCanExecuteChanged();
                NextPageCommand.RaiseCanExecuteChanged();
            }
        }

        public int PageSize
        {
            get => _pageSize;
            set
            {
                _pageSize = value <= 0 ? 20 : value;
                OnPropertyChanged();
                QueueRefresh(resetPage: true, debounceMilliseconds: 0);
            }
        }

        public int TotalCount
        {
            get => _totalCount;
            private set
            {
                _totalCount = value;
                OnPropertyChanged();
            }
        }

        public int TotalPages
        {
            get => _totalPages;
            private set
            {
                _totalPages = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageDisplayText));
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
                PreviousPageCommand.RaiseCanExecuteChanged();
                NextPageCommand.RaiseCanExecuteChanged();
            }
        }

        public bool HasItems => MarketplacePlugins.Count > 0;
        public bool IsEmpty => !IsLoading && !HasError && MarketplacePlugins.Count == 0;
        public bool CanGoPrevious => !IsLoading && Page > 1;
        public bool CanGoNext => !IsLoading && TotalPages > 0 && Page < TotalPages;
        public string PageDisplayText => $"{Resources.Page} {Math.Max(Page, 1)}/{Math.Max(TotalPages, 1)}";

        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            _isInitialized = true;
            await RefreshAsync(forceReload: true);
        }

        public Task RefreshAsync(bool forceReload = false)
        {
            return LoadPageAsync(forceReload, CancellationToken.None);
        }

        private void QueueRefresh(bool resetPage, int debounceMilliseconds)
        {
            if (!_isInitialized)
                return;

            if (resetPage)
                Page = 1;

            _loadPageCancellation?.Cancel();
            var cancellation = new CancellationTokenSource();
            _loadPageCancellation = cancellation;
            _ = RefreshAfterDelayAsync(debounceMilliseconds, cancellation.Token);
        }

        private async Task RefreshAfterDelayAsync(int debounceMilliseconds, CancellationToken cancellationToken)
        {
            try
            {
                if (debounceMilliseconds > 0)
                    await Task.Delay(debounceMilliseconds, cancellationToken);

                await LoadPageAsync(reloadCategories: false, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task ChangePageAsync(int page)
        {
            if (page < 1)
                return;

            if (TotalPages > 0 && page > TotalPages)
                return;

            Page = page;
            await LoadPageAsync(reloadCategories: false, CancellationToken.None);
        }

        private async Task LoadPageAsync(bool reloadCategories, CancellationToken externalCancellationToken)
        {
            _loadPageCancellation?.Cancel();
            _loadPageCancellation?.Dispose();
            _loadPageCancellation = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
            CancellationToken cancellationToken = _loadPageCancellation.Token;

            IsLoading = true;
            HasError = false;
            IsOffline = false;
            StatusText = Resources.Loading + "...";

            try
            {
                if (reloadCategories || Categories.Count <= 1)
                {
                    await LoadCategoriesAsync(cancellationToken);
                }

                var request = new MarketplaceSearchRequest
                {
                    Keyword = Keyword,
                    Author = Author,
                    Category = SelectedCategory.Key,
                    SortBy = SelectedSortOption.Key,
                    SortOrder = SelectedSortOption.Key == "name" ? "asc" : "desc",
                    Page = Page,
                    PageSize = PageSize,
                };

                MarketplaceSearchResult result = await _client.SearchPluginsAsync(request, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (result.TotalPages > 0 && request.Page > result.TotalPages)
                {
                    Page = result.TotalPages;
                    QueueRefresh(resetPage: false, debounceMilliseconds: 0);
                    return;
                }

                ReplaceCollection(MarketplacePlugins, result.Items);
                ReplaceCollection(PackageSuggestions, result.Items.Select(item => item.PluginId).Where(item => !string.IsNullOrWhiteSpace(item)));

                TotalCount = result.TotalCount;
                TotalPages = result.TotalPages;
                Page = result.Page <= 0 ? 1 : result.Page;
                StatusText = string.Format(Resources.MarketplacePluginCount, result.TotalCount);

                string? selectedPluginId = SelectedPlugin?.PluginId;
                MarketplacePluginSummary? nextSelection = result.Items.FirstOrDefault(item => string.Equals(item.PluginId, selectedPluginId, StringComparison.OrdinalIgnoreCase))
                    ?? result.Items.FirstOrDefault();
                SelectedPlugin = nextSelection;

                if (nextSelection == null)
                {
                    SelectedDetailContext = null;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                log.Debug($"LoadPageAsync failed: {ex.Message}");
                HasError = true;
                IsOffline = ex is HttpRequestException || ex is TaskCanceledException;
                ReplaceCollection(MarketplacePlugins, Array.Empty<MarketplacePluginSummary>());
                ReplaceCollection(PackageSuggestions, Array.Empty<string>());
                TotalCount = 0;
                TotalPages = 0;
                SelectedDetailContext = null;
                StatusText = Resources.MarketplaceLoadFailed;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadCategoriesAsync(CancellationToken cancellationToken)
        {
            List<string> categories = await _client.GetCategoriesAsync(cancellationToken);
            string currentCategory = SelectedCategory.Key;
            List<MarketplaceOptionItem> options = new()
            {
                new MarketplaceOptionItem { Key = string.Empty, DisplayName = "全部分类" }
            };
            options.AddRange(categories
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .Select(item => new MarketplaceOptionItem { Key = item, DisplayName = item }));

            _suspendAutoRefresh = true;
            try
            {
                ReplaceCollection(Categories, options);
                SelectedCategory = Categories.FirstOrDefault(item => string.Equals(item.Key, currentCategory, StringComparison.OrdinalIgnoreCase)) ?? Categories.First();
            }
            finally
            {
                _suspendAutoRefresh = false;
            }
        }

        private async Task LoadSelectedDetailAsync(MarketplacePluginSummary? summary)
        {
            _loadDetailCancellation?.Cancel();
            _loadDetailCancellation?.Dispose();
            _loadDetailCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = _loadDetailCancellation.Token;

            if (summary == null)
            {
                SelectedDetailContext = null;
                return;
            }

            try
            {
                IsLoadingDetail = true;
                MarketplacePluginDetail? detail = await _client.GetPluginDetailAsync(summary.PluginId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (detail == null)
                    return;

                PluginInfoVM? installedPlugin = _installedPluginLookup(summary.PluginId);
                var detailContext = new MarketplaceDetailContext(detail, installedPlugin);
                await detailContext.InitializeAsync();
                cancellationToken.ThrowIfCancellationRequested();
                SelectedDetailContext = detailContext;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                log.Debug($"LoadSelectedDetailAsync failed for {summary.PluginId}: {ex.Message}");
            }
            finally
            {
                IsLoadingDetail = false;
            }
        }

        private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
        {
            target.Clear();
            foreach (T item in items)
            {
                target.Add(item);
            }
        }
    }
}
