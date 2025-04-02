using System.Collections.Generic;

namespace ColorVision.UI
{
    public interface ISearchProvider
    {
        IEnumerable<ISearch> GetSearchItems();
    }

}
