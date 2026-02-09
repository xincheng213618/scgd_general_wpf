using System.Collections.Generic;

namespace ColorVision.Common.ThirdPartyApps
{
    /// <summary>
    /// Interface for providing third-party application definitions.
    /// Implement this interface in plugins to register additional apps in the Third-Party Apps window.
    /// Implementations are discovered automatically via assembly scanning.
    /// </summary>
    public interface IThirdPartyAppProvider
    {
        IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps();
    }
}
