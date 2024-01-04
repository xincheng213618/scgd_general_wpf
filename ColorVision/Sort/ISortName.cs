using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Sorts
{
    public interface ISortName
    {
        public string Name { get; set; }
    }

    public static partial class SortableExtension
    {
        public static void SortByName<T>(this ObservableCollection<T> collection, bool descending = false) where T : ISortName
        {
            var sortedItems = collection.ToList();
            sortedItems.Sort((x, y) => descending ? string.Compare(y.Name, x.Name, System.StringComparison.Ordinal) : string.Compare(x.Name, y.Name, System.StringComparison.Ordinal));

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
