using ColorVision.Common.NativeMethods;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        /// 使用自定义比较器排序
        /// </summary>
        public static void SortBy<T, TKey>(this ObservableCollection<T> collection, Func<T, TKey> keySelector, bool descending = false) 
            where TKey : IComparable<TKey>
        {
            if (collection == null || collection.Count <= 1) return;

            var sortedItems = descending 
                ? collection.OrderByDescending(keySelector).ToList()
                : collection.OrderBy(keySelector).ToList();

            collection.UpdateCollection(sortedItems);
        }

        /// <summary>
        /// 多级排序支持
        /// </summary>
        public static void SortByMultiple<T>(this ObservableCollection<T> collection, params (string PropertyName, bool Descending)[] sortCriteria)
        {
            if (collection == null || collection.Count <= 1 || sortCriteria?.Length == 0) return;

            var sortedItems = collection.AsEnumerable();

            // 从最后一个排序条件开始，因为OrderBy是稳定排序
            for (int i = sortCriteria.Length - 1; i >= 0; i--)
            {
                var (propertyName, descending) = sortCriteria[i];
                var propertyInfo = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                
                if (propertyInfo == null)
                    throw new ArgumentException($"Property '{propertyName}' not found in type '{typeof(T).Name}'.");

                if (descending)
                {
                    sortedItems = sortedItems.OrderByDescending(item => propertyInfo.GetValue(item));
                }
                else
                {
                    sortedItems = sortedItems.OrderBy(item => propertyInfo.GetValue(item));
                }
            }

            collection.UpdateCollection(sortedItems.ToList());
        }

        /// <summary>
        /// 智能排序：自动检测 Id、Key 等常见属性
        /// </summary>
        public static void SmartSort<T>(this ObservableCollection<T> collection, bool descending = false)
        {
            if (collection == null || collection.Count <= 1) return;

            var type = typeof(T);
            
            // 按优先级查找排序属性
            string[] candidateProperties = { "Id", "Key", "Name", "Title", "Order", "Index" };
            
            foreach (var propName in candidateProperties)
            {
                var prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && (typeof(IComparable).IsAssignableFrom(prop.PropertyType) || 
                    prop.PropertyType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IComparable<>))))
                {
                    collection.SortBy(propName, descending);
                    return;
                }
            }

            // 如果没找到合适的属性，尝试对象本身是否可比较
            if (typeof(IComparable<T>).IsAssignableFrom(type))
            {
                var sortedItems = descending 
                    ? collection.OrderByDescending(x => x).ToList()
                    : collection.OrderBy(x => x).ToList();
                collection.UpdateCollection(sortedItems);
            }
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

    /// <summary>
    /// 排序配置类，用于保存和恢复排序设置
    /// </summary>
    public class SortConfiguration
    {
        public string PropertyName { get; set; } = string.Empty;
        public bool Descending { get; set; }
        public DateTime LastSorted { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 排序管理器，提供更高级的排序功能
    /// </summary>
    public class SortManager<T>
    {
        private readonly ObservableCollection<T> _collection;
        private SortConfiguration? _currentSort;
        private readonly Dictionary<string, SortConfiguration> _savedSorts = new();

        public SortManager(ObservableCollection<T> collection)
        {
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }

        public SortConfiguration? CurrentSort => _currentSort;

        /// <summary>
        /// 应用排序并记录配置
        /// </summary>
        public void ApplySort(string propertyName, bool? descending = null)
        {
            // 如果是同一个属性，切换排序方向
            bool desc = descending ?? (_currentSort?.PropertyName == propertyName && !_currentSort.Descending);
            
            _collection.SortBy(propertyName, desc);
            
            _currentSort = new SortConfiguration 
            { 
                PropertyName = propertyName, 
                Descending = desc 
            };
        }

        /// <summary>
        /// 保存当前排序配置
        /// </summary>
        public void SaveSort(string name)
        {
            if (_currentSort != null)
            {
                _savedSorts[name] = new SortConfiguration 
                { 
                    PropertyName = _currentSort.PropertyName, 
                    Descending = _currentSort.Descending 
                };
            }
        }

        /// <summary>
        /// 加载保存的排序配置
        /// </summary>
        public bool LoadSort(string name)
        {
            if (_savedSorts.TryGetValue(name, out var config))
            {
                ApplySort(config.PropertyName, config.Descending);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 切换排序方向
        /// </summary>
        public void ToggleSortDirection()
        {
            if (_currentSort != null)
            {
                ApplySort(_currentSort.PropertyName, !_currentSort.Descending);
            }
        }
    }
}