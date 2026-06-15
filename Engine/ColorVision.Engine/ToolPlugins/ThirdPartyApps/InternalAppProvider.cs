using ColorVision.Common.ThirdPartyApps;
using System.Collections.Generic;

namespace ColorVision.Engine.ToolPlugins.ThirdPartyApps
{
    public class InternalAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            return new List<ThirdPartyAppInfo>();
        }
    }
}
