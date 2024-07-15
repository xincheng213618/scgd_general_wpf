using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.UI.Sorts
{
    public interface ISortName
    {
        public string? Name { get; set; }
    }

    public static partial class SortableExtension
    {
        public static void SortByName<T>(this ObservableCollection<T> collection, bool descending = false) where T : ISortName
        {
            var sortedItems = collection.ToList();
            sortedItems.Sort((x, y) => descending ? string.Compare(y.Name, x.Name, StringComparison.Ordinal) : string.Compare(x.Name, y.Name, StringComparison.Ordinal));
            collection.UpdateCollection(sortedItems);
        }
    }
}
