namespace ColorVision.UI.Marketplace
{
    public static class MarketplaceConfig
    {
        public const string DefaultServiceBaseUrl = "http://xc213618.ddns.me:9998";

        public static string ServiceBaseUrl => DefaultServiceBaseUrl;

        public static string BuildApiUrl(string relativePath)
        {
            return $"{ServiceBaseUrl}/{relativePath.TrimStart('/')}";
        }

        public static string BuildLegacyPluginUrl(string relativePath)
        {
            return BuildApiUrl($"D%3A/ColorVision/Plugins/{relativePath.TrimStart('/')}");
        }
    }
}
