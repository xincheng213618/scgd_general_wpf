using System.Collections.ObjectModel;
using System.Linq;
using ColorVision.NativeMethods;

namespace ColorVision.Common.Sorts
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
