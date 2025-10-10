using ColorVision.Common.NativeMethods;
using System.Collections.ObjectModel;
using System.Reflection;

namespace ColorVision.UI.Sorts
{
    public static partial class SortableExtension
    {


        public static void SortByProperty<TSource>(this IEnumerable<TSource> collection, string propertyName, bool descending = false)
        {
            SortByProperty1(collection, propertyName, descending);
        }





        public static void SortByProperty1(System.Collections.IEnumerable collection, string propertyName, bool descending = false)
        {
            var collectionType = (Type)collection.GetType();
            var itemType = collectionType.GetGenericArguments()[0];
            var propertyInfo = itemType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

            if (propertyInfo == null)
            {
                throw new ArgumentException($"Property '{propertyName}' not found.");
            }

            var items = collection.Cast<object>().ToList();

            if (propertyInfo.PropertyType == typeof(string))
            {
                items.Sort((x, y) =>
                {
                    var xValue = (string)propertyInfo.GetValue(x) ?? string.Empty;
                    var yValue = (string)propertyInfo.GetValue(y) ?? string.Empty;
                    return descending ? Shlwapi.CompareLogical(yValue, xValue) : Shlwapi.CompareLogical(xValue, yValue);
                });
            }
            else
            {
                items.Sort((x, y) =>
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

            UpdateCollectionDynamic(collection, items);
        }
        private static void UpdateCollectionDynamic(dynamic collection, List<object> sortedItems)
        {
            if (collection == null) return;

            for (int i = 0; i < sortedItems.Count; i++)
            {
                dynamic item = sortedItems[i];
                var currentIndex = collection.IndexOf(item);

                if (currentIndex != i)
                {
                    collection.Move(currentIndex, i);
                }
            }
        }
    }
}
