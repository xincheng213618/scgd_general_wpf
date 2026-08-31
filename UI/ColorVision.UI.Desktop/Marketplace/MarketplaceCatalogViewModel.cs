using ColorVision.Common.MVVM;
using ColorVision.UI.Desktop.Properties;
using ColorVision.UI.Marketplace;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Desktop.Marketplace
{
    public sealed class MarketplaceCatalogViewModel : ViewModelBase, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MarketplaceCatalogViewModel));
        private const int MarketplacePageSize = 100;
        private static readonly CompositeFormat MarketplacePluginCountFormat = CompositeFormat.Parse(Resources.MarketplacePluginCount);
        private readonly MarketplaceClient _client = MarketplaceClient.GetInstance();
        private readonly Func<string, PluginInfoVM?> _installedPluginLookup;
        private readonly Action<MarketplaceDetailContext?> _detailChanged;
        private CancellationTokenSource? _loadPageCancellation;
        private CancellationTokenSource? _loadDetailCancellation;
        private MarketplacePluginSummary? _selectedPlugin;
        private MarketplaceDetailContext? _selectedDetailContext;
        private bool _isInitialized;
        private bool _isLoading;
        private bool _isLoadingDetail;
        private bool _hasError;
        private bool _isOffline;
        private bool _isReplacingCatalogItems;
        private string _statusText = string.Empty;
        private int _totalCount;
        private bool _isDisposed;

        public MarketplaceCatalogViewModel(Func<string, PluginInfoVM?> installedPluginLookup, Action<MarketplaceDetailContext?> detailChanged)
        {
            _installedPluginLookup = installedPluginLookup;
            _detailChanged = detailChanged;

            MarketplacePlugins = new ObservableCollection<MarketplacePluginSummary>();
            PackageSuggestions = new ObservableCollection<string>();
            MarketplacePlugins.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(HasItems));
                OnPropertyChanged(nameof(IsEmpty));
            };
        }

        public ObservableCollection<MarketplacePluginSummary> MarketplacePlugins { get; }
        public ObservableCollection<string> PackageSuggestions { get; }

        public MarketplacePluginSummary? SelectedPlugin
        {
            get => _selectedPlugin;
            set
            {
                if (_isReplacingCatalogItems && value == null)
                    return;

                if (ReferenceEquals(_selectedPlugin, value))
                    return;

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
                if (ReferenceEquals(_selectedDetailContext, value))
                    return;

                _selectedDetailContext = value;
                OnPropertyChanged();
                _detailChanged(value);
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

        public int TotalCount
        {
            get => _totalCount;
            private set
            {
                _totalCount = value;
                OnPropertyChanged();
            }
        }

        public bool HasItems => MarketplacePlugins.Count > 0;
        public bool IsEmpty => !IsLoading && !HasError && MarketplacePlugins.Count == 0;

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
                return;

            await RefreshAsync(forceReload: true, cancellationToken);
        }

        public Task RefreshAsync(bool forceReload = false, CancellationToken cancellationToken = default)
        {
            return LoadCatalogAsync(cancellationToken);
        }

        private async Task LoadCatalogAsync(CancellationToken externalCancellationToken)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            CancelAndDispose(ref _loadPageCancellation);
            var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
            _loadPageCancellation = operationCancellation;
            CancellationToken cancellationToken = operationCancellation.Token;

            IsLoading = true;
            HasError = false;
            IsOffline = false;
            StatusText = Resources.Loading + "...";

            try
            {
                var request = new MarketplaceSearchRequest
                {
                    Keyword = string.Empty,
                    Author = string.Empty,
                    Category = string.Empty,
                    SortBy = "updated",
                    SortOrder = "desc",
                    Page = 1,
                    PageSize = MarketplacePageSize,
                };

                MarketplaceSearchResult result = await _client.SearchPluginsAsync(request, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                string? selectedPluginId = SelectedPlugin?.PluginId;
                _isReplacingCatalogItems = true;
                try
                {
                    ReplaceCollection(MarketplacePlugins, result.Items);
                    ReplaceCollection(PackageSuggestions, result.Items.Select(item => item.PluginId).Where(item => !string.IsNullOrWhiteSpace(item)));
                }
                finally
                {
                    _isReplacingCatalogItems = false;
                }

                TotalCount = result.TotalCount;
                StatusText = string.Format(null, MarketplacePluginCountFormat, result.TotalCount);

                MarketplacePluginSummary? nextSelection = selectedPluginId == null ? null
                    : result.Items.FirstOrDefault(item => string.Equals(item.PluginId, selectedPluginId, StringComparison.OrdinalIgnoreCase));
                if (ReferenceEquals(SelectedPlugin, nextSelection))
                {
                    OnPropertyChanged(nameof(SelectedPlugin));
                    if (nextSelection != null)
                        _ = LoadSelectedDetailAsync(nextSelection);
                }
                else
                    SelectedPlugin = nextSelection;

                if (nextSelection == null)
                {
                    SelectedDetailContext = null;
                }
                _isInitialized = true;
            }
            catch (OperationCanceledException)
            {
                if (ReferenceEquals(_loadPageCancellation, operationCancellation))
                    _isInitialized = false;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested || !ReferenceEquals(_loadPageCancellation, operationCancellation))
                    return;

                log.Debug($"LoadCatalogAsync failed: {ex.Message}");
                _isInitialized = false;
                HasError = true;
                IsOffline = ex is HttpRequestException || ex is TaskCanceledException;
                ReplaceCollection(MarketplacePlugins, Array.Empty<MarketplacePluginSummary>());
                ReplaceCollection(PackageSuggestions, Array.Empty<string>());
                TotalCount = 0;
                SelectedPlugin = null;
                SelectedDetailContext = null;
                StatusText = Resources.MarketplaceLoadFailed;
            }
            finally
            {
                if (ReferenceEquals(_loadPageCancellation, operationCancellation))
                {
                    IsLoading = false;
                    _loadPageCancellation = null;
                    operationCancellation.Dispose();
                }
            }
        }

        private async Task LoadSelectedDetailAsync(MarketplacePluginSummary? summary)
        {
            if (_isDisposed)
                return;

            CancelAndDispose(ref _loadDetailCancellation);
            if (summary == null)
            {
                IsLoadingDetail = false;
                SelectedDetailContext = null;
                return;
            }

            var operationCancellation = new CancellationTokenSource();
            _loadDetailCancellation = operationCancellation;
            CancellationToken cancellationToken = operationCancellation.Token;
            try
            {
                IsLoadingDetail = true;
                MarketplacePluginDetail? detail = await _client.GetPluginDetailAsync(summary.PluginId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (detail == null)
                {
                    SelectedDetailContext = null;
                    return;
                }

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
                if (!cancellationToken.IsCancellationRequested)
                    SelectedDetailContext = null;
            }
            finally
            {
                if (ReferenceEquals(_loadDetailCancellation, operationCancellation))
                {
                    IsLoadingDetail = false;
                    _loadDetailCancellation = null;
                    operationCancellation.Dispose();
                }
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

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            CancelAndDispose(ref _loadPageCancellation);
            CancelAndDispose(ref _loadDetailCancellation);
        }

        private static void CancelAndDispose(ref CancellationTokenSource? cancellationTokenSource)
        {
            if (cancellationTokenSource == null)
                return;

            try
            {
                cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }
    }
}
