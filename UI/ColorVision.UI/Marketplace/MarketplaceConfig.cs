using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ColorVision.UI.Marketplace
{
    public sealed class MarketplaceServiceConfig : ViewModelBase, IConfig
    {
        private static readonly MarketplaceServiceConfig Fallback = new();

        public static MarketplaceServiceConfig Instance
        {
            get
            {
                try
                {
                    return ConfigService.Instance.GetRequiredService<MarketplaceServiceConfig>();
                }
                catch
                {
                    return Fallback;
                }
            }
        }

        [ConfigSetting(Order = 10, Section = ConfigSettingConstants.SectionAdvancedServices, Description = "MarketplaceServiceBaseUrlDescription")]
        [DisplayName("MarketplaceServiceBaseUrl")]
        public string BaseUrl
        {
            get => _baseUrl;
            set => SetProperty(ref _baseUrl, value);
        }
        private string _baseUrl = MarketplaceConfig.DefaultServiceBaseUrl;
    }

    public static class MarketplaceConfig
    {
        public const string DefaultServiceBaseUrl = "http://xc213618.ddns.me:9998";

        public static string ServiceBaseUrl => NormalizeBaseUrl(MarketplaceServiceConfig.Instance.BaseUrl);

        public static string BuildApiUrl(string relativePath)
        {
            return $"{ServiceBaseUrl}/{relativePath.TrimStart('/')}";
        }

        public static string BuildLegacyPluginUrl(string relativePath)
        {
            return BuildApiUrl($"D%3A/ColorVision/Plugins/{relativePath.TrimStart('/')}");
        }

        private static string NormalizeBaseUrl(string? baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return DefaultServiceBaseUrl;

            return baseUrl.Trim().TrimEnd('/');
        }
    }
}
