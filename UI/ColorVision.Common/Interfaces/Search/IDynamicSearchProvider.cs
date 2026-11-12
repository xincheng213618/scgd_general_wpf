using System.Collections.Generic;

namespace ColorVision.UI
{
    /// <summary>
    /// Supplies query-dependent search results without materializing an
    /// entire external index every time the global search box receives focus.
    /// </summary>
    public interface IDynamicSearchProvider
    {
        IEnumerable<ISearch> Search(string query, int limit);
    }
}
