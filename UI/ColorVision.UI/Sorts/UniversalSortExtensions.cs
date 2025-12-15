using ColorVision.Common.NativeMethods;
using System.Collections.ObjectModel;
using System.Reflection;

namespace ColorVision.UI.Sorts
{
    /// <summary>
    /// 通用排序扩展，不需要实现特定接口
    /// </summary>
    public static class UniversalSortExtensions
    {
        /// <summary>
        /// 通用属性排序，支持任意类型
        /// </summary>
        /// <typeparam name="T">集合元素类型</typeparam>
        /// <param name="collection">要排序的集合</param>
        /// <param name="propertyName">属性名称</param>
        /// <param name="descending">是否降序</param>
        public static void SortBy<T>(this ObservableCollection<T> collection, string propertyName, bool descending = false)
        {
            if (collection == null || collection.Count <= 1) return;

            var propertyInfo = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (propertyInfo == null)
                throw new ArgumentException($"Property '{propertyName}' not found in type '{typeof(T).Name}'.");

            var sortedItems = collection.ToList();

            // 特殊处理字符串类型，使用逻辑排序
            if (propertyInfo.PropertyType == typeof(string))
            {
                sortedItems.Sort((x, y) =>
                {
                    var xValue = (string)propertyInfo.GetValue(x) ?? string.Empty;
                    var yValue = (string)propertyInfo.GetValue(y) ?? string.Empty;
                    return descending ? Shlwapi.CompareLogical(yValue, xValue) : Shlwapi.CompareLogical(xValue, yValue);
                });
            }
            // 处理可比较类型
            else if (typeof(IComparable).IsAssignableFrom(propertyInfo.PropertyType) || 
                     propertyInfo.PropertyType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IComparable<>)))
            {
                sortedItems.Sort((x, y) =>
                {
                    var xValue = propertyInfo.GetValue(x) as IComparable;
                    var yValue = propertyInfo.GetValue(y) as IComparable;

                    if (xValue == null && yValue == null) return 0;
                    if (xValue == null) return descending ? 1 : -1;
                    if (yValue == null) return descending ? -1 : 1;

                    return descending ? yValue.CompareTo(xValue) : xValue.CompareTo(yValue);
                });
            }
            else
            {
                throw new InvalidOperationException($"Property '{propertyName}' of type '{propertyInfo.PropertyType.Name}' is not comparable.");
            }

            collection.UpdateCollection(sortedItems);
        }

        /// <summary>
        /// 添加唯一元素（通用版本）
        /// </summary>
        public static void AddUniqueBy<T, TKey>(this ObservableCollection<T> collection, T item, Func<T, TKey> keySelector, bool insertAtBeginning = false)
        {
            if (collection.Any(existingItem => EqualityComparer<TKey>.Default.Equals(keySelector(existingItem), keySelector(item))))
                return;

            if (insertAtBeginning)
                collection.Insert(0, item);
            else
                collection.Add(item);
        }

        /// <summary>
        /// 更新集合元素顺序的内部方法
        /// </summary>
        private static void UpdateCollection<T>(this ObservableCollection<T> collection, List<T> sortedItems)
        {
            if (collection == null) return;

            for (int i = 0; i < sortedItems.Count; i++)
            {
                var item = sortedItems[i];
                var currentIndex = collection.IndexOf(item);

                if (currentIndex != i && currentIndex >= 0)
                {
                    collection.Move(currentIndex, i);
                }
            }
        }
    }
}