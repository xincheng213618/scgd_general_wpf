using System.Collections.ObjectModel;
using ColorVision.Common.NativeMethods;

namespace ColorVision.UI.Sorts
{
    public interface ISortFilePath
    {
        public string? FilePath { get; set; }
    }

    public static partial class SortableExtension
    {
        public static void SortByFilePath<T>(this ObservableCollection<T> collection, bool descending = false) where T : ISortFilePath
        {
            var sortedItems = collection.ToList();
            sortedItems.Sort((x, y) => descending ? Shlwapi.CompareLogical(y.FilePath??string.Empty, x.FilePath ?? string.Empty) : Shlwapi.CompareLogical(x.FilePath ?? string.Empty, y.FilePath ?? string.Empty));
            collection.UpdateCollection(sortedItems);
        }
    }
}
