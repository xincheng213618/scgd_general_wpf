﻿using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Sorts
{
    public interface ISortID
    {
        public int ID { get; set; }
    }

    public static partial class SortableExtension
    {
        public static void SortByID<T>(this ObservableCollection<T> collection, bool descending = false) where T : ISortID
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
    }
}
