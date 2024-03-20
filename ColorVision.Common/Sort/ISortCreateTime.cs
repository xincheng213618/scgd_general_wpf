using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Common.Sorts
{
    public interface ISortCreateTime
    {
        DateTime? CreateTime { get; set; }
    }

    public interface ISortRecvTime
    {
        DateTime? RecvTime { get; set; }
    }

    public static partial class SortableExtension
    {
        public static void SortByCreateTime<T>(this ObservableCollection<T> collection, bool descending = false) where T : ISortCreateTime
        {
            var sortedItems = collection.ToList();
            sortedItems.Sort((x, y) => descending ? y.CreateTime?.CompareTo(x.CreateTime) ?? 0 : x.CreateTime?.CompareTo(y.CreateTime) ?? 0);

            // 然后，我们在ObservableCollection中重新排列元素，而不是删除并重新添加
            for (int i = 0; i < sortedItems.Count; i++)
            {
                // 获取应该在当前位置的项
                var item = sortedItems[i];
                // 获取该项在ObservableCollection中的当前位置
                int currentIndex = collection.IndexOf(item);
                if (currentIndex != i)
                {
                    // 如果项不在正确的位置，我们将其移动到正确的位置
                    collection.Move(currentIndex, i);
                }
            }
        }

        public static void SortByRecvTime<T>(this ObservableCollection<T> collection, bool descending = false) where T : ISortRecvTime
        {
            var sortedItems = collection.ToList();
            sortedItems.Sort((x, y) => descending ? y.RecvTime?.CompareTo(x.RecvTime) ?? 0 : x.RecvTime?.CompareTo(y.RecvTime) ?? 0);

            // 然后，我们在ObservableCollection中重新排列元素，而不是删除并重新添加
            for (int i = 0; i < sortedItems.Count; i++)
            {
                // 获取应该在当前位置的项
                var item = sortedItems[i];
                // 获取该项在ObservableCollection中的当前位置
                int currentIndex = collection.IndexOf(item);
                if (currentIndex != i)
                {
                    // 如果项不在正确的位置，我们将其移动到正确的位置
                    collection.Move(currentIndex, i);
                }
            }
        }
    }
}
