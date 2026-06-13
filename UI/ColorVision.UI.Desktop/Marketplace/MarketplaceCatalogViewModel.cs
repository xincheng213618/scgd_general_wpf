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

            _isInitialized = true;
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
            _loadPageCancellation = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
            CancellationToken cancellationToken = _loadPageCancellation.Token;

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

                ReplaceCollection(MarketplacePlugins, result.Items);
                ReplaceCollection(PackageSuggestions, result.Items.Select(item => item.PluginId).Where(item => !string.IsNullOrWhiteSpace(item)));

                TotalCount = result.TotalCount;
                StatusText = string.Format(null, MarketplacePluginCountFormat, result.TotalCount);

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
                log.Debug($"LoadCatalogAsync failed: {ex.Message}");
                HasError = true;
                IsOffline = ex is HttpRequestException || ex is TaskCanceledException;
                ReplaceCollection(MarketplacePlugins, Array.Empty<MarketplacePluginSummary>());
                ReplaceCollection(PackageSuggestions, Array.Empty<string>());
                TotalCount = 0;
                SelectedDetailContext = null;
                StatusText = Resources.MarketplaceLoadFailed;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadSelectedDetailAsync(MarketplacePluginSummary? summary)
        {
            if (_isDisposed)
                return;

            CancelAndDispose(ref _loadDetailCancellation);
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
                SelectedDetailContext = null;
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
