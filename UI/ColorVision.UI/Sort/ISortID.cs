using ColorVision.Common.NativeMethods;
using System.Collections.ObjectModel;
using System.Reflection;

namespace ColorVision.UI.Sorts
{
    public interface ISortID
    {
        public int Id { get;  }
    }

    public interface ISortKey
    {
        public string Key { get; }
    }

    public static partial class SortableExtension
    {
        public static void AddUnique<T>(this ObservableCollection<T> collection,T item, bool InsertAtBeginning = false) where T : ISortID
        {
            if (!collection.Any(existingItem => existingItem.Id == item.Id))
            {
                if (!InsertAtBeginning)
                {
                    collection.Add(item);
                }
                else
                {
                    collection.Insert(0,item);
                }
            }
        }
        private static void UpdateCollection<T>(this ObservableCollection<T> collection, List<T> sortedItems)
        {
            if (collection == null) return;

            for (int i = 0; i < sortedItems.Count; i++)
            {
                var item = sortedItems[i];
                var currentIndex = collection.IndexOf(item);

                if (currentIndex != i)
                {
                    collection.Move(currentIndex, i);
                }
            }
        }

        public static void SortByKey<T>(this ObservableCollection<T> collection, bool descending = false) where T : ISortKey
        {
            var sortedItems = collection.ToList();
            sortedItems.Sort((x, y) => descending ? Shlwapi.CompareLogical(y.Key ?? string.Empty, x.Key ?? string.Empty) : Shlwapi.CompareLogical(x.Key ?? string.Empty, y.Key ?? string.Empty));
            collection.UpdateCollection(sortedItems);
        }

        public static void SortByID<T>(this ObservableCollection<T> collection, bool descending = false) where T : ISortID
        {
            var sortedItems = collection.ToList();
            sortedItems.Sort((x, y) => descending ? y.Id.CompareTo(x.Id) : x.Id.CompareTo(y.Id));
            collection.UpdateCollection(sortedItems);
        }

        public static void SortByProperty<T>(this ObservableCollection<T> collection, string propertyName, bool descending = false)
        {
            var propertyInfo = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (propertyInfo == null)
            {
                throw new ArgumentException($"Property '{propertyName}' not found.");
            }

            var sortedItems = collection.ToList();

            if (propertyInfo.PropertyType == typeof(string))
            {
                sortedItems.Sort((x, y) =>
                {
                    var xValue = (string)propertyInfo.GetValue(x) ?? string.Empty;
                    var yValue = (string)propertyInfo.GetValue(y) ?? string.Empty;
                    return descending ? Shlwapi.CompareLogical(yValue, xValue) : Shlwapi.CompareLogical(xValue, yValue);
                });
            }
            else
            {
                sortedItems.Sort((x, y) =>
                {
                    var xValue = propertyInfo.GetValue(x) as IComparable;
                    var yValue = propertyInfo.GetValue(y) as IComparable;

                    if (xValue == null || yValue == null)
                    {
                        throw new InvalidOperationException("Property values must be comparable.");
                    }

                    return descending ? yValue.CompareTo(xValue) : xValue.CompareTo(yValue);
                });
            }

            collection.UpdateCollection(sortedItems);
        }
    }
}
