using System.Collections.Generic;

namespace ColorVision.Common.ThirdPartyApps
{
    /// <summary>
    /// Provides application and tool definitions for the centralized Apps &amp; Tools launcher.
    /// Implement this interface in plugins to register internal, system, external, or custom tools.
    /// Implementations are discovered automatically via assembly scanning.
    /// </summary>
    public interface IThirdPartyAppProvider
    {
        IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps();
    }

    public interface IThirdPartyAppCacheAwareProvider : IThirdPartyAppProvider
    {
        IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps(bool forceRefresh);
    }
}
