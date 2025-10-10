using ColorVision.Common.NativeMethods;
using System.Collections.ObjectModel;
using System.Reflection;

namespace ColorVision.UI.Sorts
{
    public static partial class SortableExtension
    {

        public static void SortByProperty(this System.Collections.IEnumerable collection, string propertyName, bool descending = false)
        {
            var collectionType = (Type)collection.GetType();
            var itemType = collectionType.GetGenericArguments()[0];
            var propertyInfo = itemType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

            if (propertyInfo == null)
            {
                throw new ArgumentException($"Property '{propertyName}' not found.");
            }

            var sortedItems = collection.Cast<object>().ToList();

            if (propertyInfo.PropertyType == typeof(string))
            {
                sortedItems.Sort((x, y) =>
                {
                    var xValue = (string)propertyInfo.GetValue(x) ?? string.Empty;
                    var yValue = (string)propertyInfo.GetValue(y) ?? string.Empty;
                    return descending ? Shlwapi.CompareLogical(yValue, xValue) : Shlwapi.CompareLogical(xValue, yValue);
                });
            }
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

            UpdateCollectionDynamic(collection, sortedItems);
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
