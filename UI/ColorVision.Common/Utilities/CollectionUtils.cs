#pragma warning disable CS8602
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Common.Utilities
{
    /// <summary>
    /// ObservableCollection 类的一些扩展函数。
    /// </summary>
    public static class CollectionUtils
    {
        /// <summary>
        /// RemoveAll for ObservableCollections
        /// </summary>
        /// https://stackoverflow.com/questions/5118513/removeall-for-observablecollections
        public static int RemoveAll<T>(this Collection<T> This, Func<T, bool> condition)
        {
            var itemsToRemove = This.Where(condition).ToList();

            foreach (var itemToRemove in itemsToRemove)
            {
                This.Remove(itemToRemove);
            }

            return itemsToRemove.Count;
        }

        /// <summary>
        /// 对集合进行升序排序。
        /// </summary>
        public static void Sort<T>(this ObservableCollection<T> This)
        {
            List<T> l = This.ToList();
            l.Sort();   // 使用缺省比较函数
            int index = 0;
            foreach (var v in l)
                This.Move(This.IndexOf(v), index++);
        }

        /// <summary>
        /// 对集合进行升序排序。
        /// </summary>
        public static void Sort<T>(this ObservableCollection<T> This, Comparison<T> comparison)
        {
            List<T> l = This.ToList();
            l.Sort(comparison);
            int index = 0;
            foreach (var v in l)
                This.Move(This.IndexOf(v), index++);
        }

        /// <summary>
        /// 对集合进行升序排序。
        /// </summary>
        public static void Sort<T>(this ObservableCollection<T> This, IComparer<T> comparer)
        {
            List<T> l = This.ToList();
            l.Sort(comparer);
            int index = 0;
            foreach (var v in l)
                This.Move(This.IndexOf(v), index++);
        }

        /// <summary>
        /// 按照给定的排序规则，生成一个新的数组。
        /// </summary>
        public static void Sort<T>(this Collection<T> This, out ObservableCollection<T> dataList, IComparer<T> comparer)
        {
            dataList = new ObservableCollection<T>();
            foreach (var item in This)
            {
                dataList.SortedAdd(item, comparer);
            }
        }

        /// <summary>
        /// 按照升序的方式，将项目加入集合。
        /// </summary>
        public static void SortedAdd<T>(this Collection<T> This, T item)
        {
            if (This is null) return;

            int index = This.BinarySearch(item);
            if (index < 0) index = -index - 1;
            This.Insert(index, item);
        }

        /// <summary>
        /// 按照升序的方式，将项目加入集合。
        /// </summary>
        public static void SortedAdd<T>(this Collection<T> This, T item, Comparison<T> comparison)
        {
            int index = This.BinarySearch(item, comparison);
            if (index < 0) index = -index - 1;
            This.Insert(index, item);
        }

        /// <summary>
        /// 按照升序的方式，将项目加入集合。
        /// </summary>
        public static void SortedAdd<T>(this Collection<T> This, T item, IComparer<T> comparer)
        {
            int index = This.BinarySearch(item, comparer);
            if (index < 0) index = -index - 1;
            This.Insert(index, item);
        }

        /// <summary>
        /// 按照升序的方式，将项目加入集合。
        /// </summary>
        public static void SortedAdd<T>(this Collection<T> This, IEnumerable<T> items)
        {
            if (items != null)
            {
                foreach (var v in items)
                    This.SortedAdd(v);
            }
        }

        /// <summary>
        /// 按照升序的方式，将项目加入集合。
        /// </summary>
        public static void SortedAdd<T>(this Collection<T> This, IEnumerable<T> items, Comparison<T> comparison)
        {
            if (items != null)
            {
                foreach (var v in items)
                    This.SortedAdd(v, comparison);
            }
        }

        /// <summary>
        /// 按照升序的方式，将项目加入集合。
        /// </summary>
        public static void SortedAdd<T>(this Collection<T> This, IEnumerable<T> items, IComparer<T> comparer)
        {
            if (items != null)
            {
                foreach (var v in items)
                    This.SortedAdd(v, comparer);
            }
        }

        /// <summary>
        /// 在升序排序的集合中，使用二分法查找指定的项目。
        /// </summary>
        /// <returns>
        ///     如果指定项目在集合中存在的话（==），则返回集合中的序号（从0开始）；
        ///     如果指定项目不存在的话，则返回集合中下一个比指定项目大的元素的序号负数，如果指定的项目最大，
        /// 则返回 -Count。此时对返回值 -ret - 1，就得到了数组的插入位置。
        /// </returns>
        public static int BinarySearch<T>(this Collection<T> This, T item)
        {
            if (This is null) return -1;

            if (item is IComparable<T>)
            {
                IComparable<T> _item = item as IComparable<T>;
                int left = 0, right = This.Count - 1, mid;
                while (left <= right)
                {
                    mid = left + (right - left) / 2;
                    if (item.Equals(This[mid]))
                        return mid;
                    if (_item.CompareTo(This[mid]) >= 0)
                        left = mid + 1;
                    else
                        right = mid - 1;
                }
                return -left - 1;
            }
            else if (item is IComparable)
            {
                IComparable _item = item as IComparable;
                int left = 0, right = This.Count - 1, mid;
                while (left <= right)
                {
                    mid = left + (right - left) / 2;
                    if (item.Equals(This[mid]))
                        return mid;
                    if (_item.CompareTo(This[mid]) >= 0)
                        left = mid + 1;
                    else
                        right = mid - 1;
                }
                return -left - 1;
            }
            else
            {
                int index = This.IndexOf(item);
                if (index >= 0) return index;
                return -This.Count - 1;
            }
        }

        /// <summary>
        /// 在升序排序的集合中，使用二分法查找指定的项目。
        /// </summary>
        /// <returns>
        ///     如果指定项目在集合中存在的话（==），则返回集合中的序号（从0开始）；
        ///     如果指定项目不存在的话，则返回集合中下一个比指定项目大的元素的序号负数，如果指定的项目最大，
        /// 则返回 -Count。此时对返回值 -ret - 1，就得到了数组的插入位置。
        /// </returns>
        public static int BinarySearch<T>(this Collection<T> This, T item, Comparison<T> comparison)
        {
            int left = 0, right = This.Count - 1, mid;
            while (left <= right)
            {
                mid = left + (right - left) / 2;
                if (item.Equals(This[mid]))
                    return mid;
                if (comparison(item, This[mid]) >= 0)
                    left = mid + 1;
                else
                    right = mid - 1;
            }
            return -left - 1;
        }

        /// <summary>
        /// 在升序排序的集合中，使用二分法查找指定的项目。
        /// </summary>
        /// <returns>
        ///     如果指定项目在集合中存在的话（==），则返回集合中的序号（从0开始）；
        ///     如果指定项目不存在的话，则返回集合中下一个比指定项目大的元素的序号负数，如果指定的项目最大，
        /// 则返回 -Count。此时对返回值 -ret - 1，就得到了数组的插入位置。
        /// </returns>
        public static int BinarySearch<T>(this Collection<T> This, T item, IComparer<T> comparer)
        {
            int left = 0, right = This.Count - 1, mid;
            while (left <= right)
            {
                mid = left + (right - left) / 2;
                if (item.Equals(This[mid]))
                    return mid;
                if (comparer.Compare(item, This[mid]) >= 0)
                    left = mid + 1;
                else
                    right = mid - 1;
            }
            return -left - 1;
        }
    }

}
