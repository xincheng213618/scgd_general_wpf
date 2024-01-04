using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Sort
{
    public static class SortableExtension
    {
        public static void SortById<T>(this ObservableCollection<T> collection,bool descending = false) where T : ISortID
        {
            var sortedItems = collection.ToList();
            sortedItems.Sort((x, y) => descending ? y.ID.CompareTo(x.ID) : x.ID.CompareTo(y.ID));

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

        public static void SortByCreateTime<T>(this ObservableCollection<T> collection, bool descending = false) where T : ISortCreateTime
        {
            var sortedItems = collection.ToList();
            sortedItems.Sort((x, y) => descending ? y.CreateTime?.CompareTo(x.CreateTime)??0 : x.CreateTime?.CompareTo(y.CreateTime)??0);

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

        public static void SortByBatch<T>(this ObservableCollection<T> collection, bool descending = false) where T : ISortBatch
        {
            var sortedItems = collection.ToList();
            sortedItems.Sort((x, y) => descending ?  string.Compare(y.Batch,x.Batch,System.StringComparison.Ordinal): string.Compare(x.Batch, y.Batch, System.StringComparison.Ordinal));

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

        public static void SortByName<T>(this ObservableCollection<T> collection, bool descending = false) where T :ISortName
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


        public static void SortByBatchID<T>(this ObservableCollection<T> collection, bool descending = false) where T : ISortBatchID
        {
            var sortedItems = collection.ToList();
            sortedItems.Sort((x, y) => descending ? y.BatchID?.CompareTo(x.BatchID)??0 : x.BatchID?.CompareTo(y.BatchID)??0);

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
