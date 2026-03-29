using ColorVision.Common.MVVM;

namespace ColorVision.UI.Marketplace
{
    /// <summary>
    /// Configuration for the plugin marketplace backend connection.
    /// Falls back to legacy file-server mode when MarketplaceApiUrl is empty.
    /// </summary>
    public class MarketplaceConfig : ViewModelBase, IConfig
    {
        public static MarketplaceConfig Instance => ConfigService.Instance.GetRequiredService<MarketplaceConfig>();

        /// <summary>
        /// Base URL for the marketplace API backend.
        /// When empty, the client falls back to the legacy file-server (PluginUpdatePath).
        /// Example: "http://localhost:5000" or "https://marketplace.colorvision.com"
        /// </summary>
        public string MarketplaceApiUrl { get => _MarketplaceApiUrl; set { _MarketplaceApiUrl = value; OnPropertyChanged(); } }
        private string _MarketplaceApiUrl = "http://xc213618.ddns.me:9998/";

        /// <summary>
        /// Whether to prefer the marketplace API over the legacy file-server for version checks and downloads.
        /// </summary>
        public bool UseMarketplaceApi { get => _UseMarketplaceApi; set { _UseMarketplaceApi = value; OnPropertyChanged(); } }
        private bool _UseMarketplaceApi;
    }
}
