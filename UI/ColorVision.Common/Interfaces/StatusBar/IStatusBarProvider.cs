using System.Collections.Generic;

namespace ColorVision.UI
{
    public interface IStatusBarProvider
    {
        IEnumerable<StatusBarMeta> GetStatusBarIconMetadata();
    }
}
