using System.Collections.ObjectModel;
using System.Linq;
using ColorVision.NativeMethods;

namespace ColorVision.UI.Sorts
{
    public interface ISortBatch
    {
        string? Batch { get; set; }
    }

    public interface ISortBatchID
    {
        int? BatchID { get; set; }
    }

    public static partial class SortableExtension
    {
        public static void SortByBatch<T>(this ObservableCollection<T> collection, bool descending = false) where T : ISortBatch
        {
            var sortedItems = collection.ToList();
            sortedItems.Sort((x, y) => descending ? Shlwapi.CompareLogical(y.Batch ?? string.Empty, x.Batch ?? string.Empty) : Shlwapi.CompareLogical(x.Batch ?? string.Empty, y.Batch ?? string.Empty));

            int index = 0;
            while (index < sortedItems.Count)
            {
                if (!collection[index].Equals(sortedItems[index]))
                {
                    // 查找当前位置的正确项在未排序集合中的位置
                    var correctItem = sortedItems[index];
                    var currentIndex = collection.IndexOf(correctItem);

                    // 交换集合中的项
                    collection.Move(currentIndex, index);
                }
                else
                {
                    index++;
                }
            }
        }


        public static void SortByBatchID<T>(this ObservableCollection<T> collection, bool descending = false) where T : ISortBatchID
        {
            var sortedItems = collection.ToList();
            sortedItems.Sort((x, y) => descending ? y.BatchID?.CompareTo(x.BatchID) ?? 0 : x.BatchID?.CompareTo(y.BatchID) ?? 0);

            int index = 0;
            while (index < sortedItems.Count)
            {
                if (!collection[index].Equals(sortedItems[index]))
                {
                    // 查找当前位置的正确项在未排序集合中的位置
                    var correctItem = sortedItems[index];
                    var currentIndex = collection.IndexOf(correctItem);

                    // 交换集合中的项
                    collection.Move(currentIndex, index);
                }
                else
                {
                    index++;
                }
            }
        }
    }
}
