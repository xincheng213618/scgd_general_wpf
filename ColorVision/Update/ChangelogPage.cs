using ColorVision.Common.Utilities;
using ColorVision.UI.Marketplace;

namespace ColorVision.Update
{
    public static class ChangelogPage
    {
        public static string Url => MarketplaceConfig.BuildApiUrl("changelog");

        public static void Open()
        {
            PlatformHelper.Open(Url);
        }
    }
}
