#pragma warning disable CA1309,CA1859
using ColorVision.Common.NativeMethods;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.UI.Sorts
{
    public static partial class SortableExtension
    {
        public static readonly DependencyProperty SortMemberPathProperty = DependencyProperty.RegisterAttached(
            "SortMemberPath",
            typeof(string),
            typeof(SortableExtension),
            new PropertyMetadata(null));

        private static readonly DependencyProperty IsSortDirectionInitializedProperty = DependencyProperty.RegisterAttached(
            "IsSortDirectionInitialized",
            typeof(bool),
            typeof(SortableExtension),
            new PropertyMetadata(false));

        private static readonly DependencyProperty IsSortDescendingProperty = DependencyProperty.RegisterAttached(
            "IsSortDescending",
            typeof(bool),
            typeof(SortableExtension),
            new PropertyMetadata(false));

        public static void SetSortMemberPath(DependencyObject element, string? value) => element.SetValue(SortMemberPathProperty, value);

        public static string? GetSortMemberPath(DependencyObject element) => (string?)element.GetValue(SortMemberPathProperty);

        public static bool SortByGridViewColumn<T>(this IEnumerable collection, object sender, ObservableCollection<GridViewColumnVisibility>? gridViewColumnVisibilitys = null, ResourceManager? resourceManager = null)
        {
            ArgumentNullException.ThrowIfNull(collection);

            if (sender is not GridViewColumnHeader gridViewColumnHeader || gridViewColumnHeader.Column == null)
                return false;

            if (!TryGetSortPropertyName(typeof(T), gridViewColumnHeader.Column, gridViewColumnHeader.Content?.ToString(), resourceManager, out string propertyName))
                return false;

            bool descending = ToggleSortDirection(gridViewColumnHeader, gridViewColumnVisibilitys);
            collection.SortByProperty(propertyName, descending);
            return true;
        }

        public static void SortByProperty(this IEnumerable collection, string propertyName, bool descending = false)
        {
            ArgumentNullException.ThrowIfNull(collection);

            var items = collection.Cast<object>().ToList();
            if (items.Count == 0)
                return;

            var itemType = GetCollectionItemType(collection) ?? items.FirstOrDefault(item => item != null)?.GetType();
            if (itemType == null)
                return;

            var propertyPath = ResolvePropertyPath(itemType, propertyName);

            var sortedItems = items
                .Select((item, index) => new SortItem(item, GetPropertyValue(item, propertyPath), index))
                .OrderBy(item => item, new SortItemComparer(descending))
                .Select(item => item.Item)
                .ToList();

            UpdateCollection(collection, sortedItems);
        }

        private static bool TryGetSortPropertyName(Type itemType, GridViewColumn column, string? headerText, ResourceManager? resourceManager, out string propertyName)
        {
            propertyName = string.Empty;

            if (TryUsePropertyPath(itemType, GetSortMemberPath(column), out propertyName))
                return true;

            if (TryGetBindingPath(column.DisplayMemberBinding, out string displayMemberPath) && TryUsePropertyPath(itemType, displayMemberPath, out propertyName))
                return true;

            if (TryGetCellTemplateBindingPath(column.CellTemplate, out string cellTemplatePath) && TryUsePropertyPath(itemType, cellTemplatePath, out propertyName))
                return true;

            if (string.IsNullOrWhiteSpace(headerText))
                return false;

            var propertyInfo = FindPropertyByHeader(itemType, headerText, resourceManager);
            if (propertyInfo == null)
                return false;

            propertyName = propertyInfo.Name;
            return true;
        }

        private static bool TryUsePropertyPath(Type itemType, string? candidatePath, out string propertyName)
        {
            propertyName = string.Empty;
            if (string.IsNullOrWhiteSpace(candidatePath))
                return false;

            if (!TryResolvePropertyPath(itemType, candidatePath, out _))
                return false;

            propertyName = candidatePath;
            return true;
        }

        private static PropertyInfo? FindPropertyByHeader(Type itemType, string headerText, ResourceManager? resourceManager)
        {
            foreach (var propertyInfo in itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (string.Equals(propertyInfo.Name, headerText, StringComparison.CurrentCultureIgnoreCase))
                    return propertyInfo;

                var displayName = propertyInfo.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
                if (string.IsNullOrWhiteSpace(displayName))
                    continue;

                if (string.Equals(displayName, headerText, StringComparison.CurrentCultureIgnoreCase))
                    return propertyInfo;

                var localizedDisplayName = resourceManager?.GetString(displayName, CultureInfo.CurrentUICulture) ?? displayName;
                if (string.Equals(localizedDisplayName, headerText, StringComparison.CurrentCultureIgnoreCase))
                    return propertyInfo;
            }

            return null;
        }

        private static bool TryGetCellTemplateBindingPath(DataTemplate? cellTemplate, out string propertyName)
        {
            propertyName = string.Empty;
            if (cellTemplate == null)
                return false;

            try
            {
                return cellTemplate.LoadContent() is DependencyObject content && TryFindBindingPath(content, out propertyName);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryFindBindingPath(DependencyObject element, out string propertyName)
        {
            propertyName = string.Empty;

            if (element is TextBlock textBlock && TryGetBindingPath(BindingOperations.GetBindingBase(textBlock, TextBlock.TextProperty), out propertyName))
                return true;

            if (element is TextBox textBox && TryGetBindingPath(BindingOperations.GetBindingBase(textBox, TextBox.TextProperty), out propertyName))
                return true;

            if (element is ToggleButton toggleButton && TryGetBindingPath(BindingOperations.GetBindingBase(toggleButton, ToggleButton.IsCheckedProperty), out propertyName))
                return true;

            if (element is ContentControl contentControl)
            {
                if (TryGetBindingPath(BindingOperations.GetBindingBase(contentControl, ContentControl.ContentProperty), out propertyName))
                    return true;

                if (contentControl.Content is DependencyObject content && TryFindBindingPath(content, out propertyName))
                    return true;
            }

            if (element is Decorator decorator && decorator.Child != null && TryFindBindingPath(decorator.Child, out propertyName))
                return true;

            if (element is Panel panel)
            {
                foreach (UIElement child in panel.Children)
                {
                    if (TryFindBindingPath(child, out propertyName))
                        return true;
                }
            }

            int childCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childCount; i++)
            {
                if (TryFindBindingPath(VisualTreeHelper.GetChild(element, i), out propertyName))
                    return true;
            }

            return false;
        }

        private static bool TryGetBindingPath(BindingBase? bindingBase, out string propertyName)
        {
            propertyName = string.Empty;

            if (bindingBase is Binding binding)
            {
                var path = binding.Path?.Path;
                if (!string.IsNullOrWhiteSpace(path) && path != ".")
                {
                    propertyName = path;
                    return true;
                }
            }

            if (bindingBase is PriorityBinding priorityBinding)
            {
                foreach (var childBinding in priorityBinding.Bindings)
                {
                    if (TryGetBindingPath(childBinding, out propertyName))
                        return true;
                }
            }

            return false;
        }

        private static bool ToggleSortDirection(GridViewColumnHeader gridViewColumnHeader, ObservableCollection<GridViewColumnVisibility>? gridViewColumnVisibilitys)
        {
            var column = gridViewColumnHeader.Column;
            bool initialized = (bool)column.GetValue(IsSortDirectionInitializedProperty);
            bool descending = initialized && !(bool)column.GetValue(IsSortDescendingProperty);

            column.SetValue(IsSortDirectionInitializedProperty, true);
            column.SetValue(IsSortDescendingProperty, descending);

            var columnVisibility = gridViewColumnVisibilitys?.FirstOrDefault(item => ReferenceEquals(item.GridViewColumn, column))
                ?? gridViewColumnVisibilitys?.FirstOrDefault(item => string.Equals(item.ColumnName?.ToString(), gridViewColumnHeader.Content?.ToString(), StringComparison.CurrentCulture));
            if (columnVisibility != null)
                columnVisibility.IsSortD = descending;

            return descending;
        }

        private static Type? GetCollectionItemType(IEnumerable collection)
        {
            var collectionType = collection.GetType();
            if (collectionType.IsArray)
                return collectionType.GetElementType();

            if (collectionType.IsGenericType && collectionType.GetGenericArguments().Length == 1)
                return collectionType.GetGenericArguments()[0];

            return collectionType.GetInterfaces()
                .FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0];
        }

        private static IReadOnlyList<PropertyInfo> ResolvePropertyPath(Type itemType, string propertyName)
        {
            if (!TryResolvePropertyPath(itemType, propertyName, out var propertyPath))
                throw new ArgumentException($"Property '{propertyName}' not found.", nameof(propertyName));

            return propertyPath;
        }

        private static bool TryResolvePropertyPath(Type itemType, string propertyName, out IReadOnlyList<PropertyInfo> propertyPath)
        {
            var properties = new List<PropertyInfo>();
            Type currentType = itemType;

            foreach (var segment in propertyName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var propertyInfo = currentType.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (propertyInfo == null)
                {
                    propertyPath = Array.Empty<PropertyInfo>();
                    return false;
                }

                properties.Add(propertyInfo);
                currentType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
            }

            propertyPath = properties;
            return properties.Count > 0;
        }

        private static object? GetPropertyValue(object? item, IReadOnlyList<PropertyInfo> propertyPath)
        {
            object? value = item;
            foreach (var propertyInfo in propertyPath)
            {
                if (value == null)
                    return null;

                value = propertyInfo.GetValue(value);
            }

            return value;
        }

        private static int CompareValues(object? xValue, object? yValue)
        {
            if (ReferenceEquals(xValue, yValue))
                return 0;
            if (xValue == null)
                return -1;
            if (yValue == null)
                return 1;

            if (xValue is string xString && yValue is string yString)
                return Shlwapi.CompareLogical(xString, yString);

            if (xValue is IComparable comparable)
            {
                try
                {
                    return comparable.CompareTo(yValue);
                }
                catch (ArgumentException)
                {
                }
            }

            return Shlwapi.CompareLogical(Convert.ToString(xValue, CultureInfo.CurrentCulture) ?? string.Empty, Convert.ToString(yValue, CultureInfo.CurrentCulture) ?? string.Empty);
        }

        private static void UpdateCollection(IEnumerable collection, IReadOnlyList<object> sortedItems)
        {
            if (collection is not IList list)
                throw new NotSupportedException("Collection sorting requires IList or a collection with Move(int, int).");

            var moveMethod = collection.GetType().GetMethod(nameof(ObservableCollection<object>.Move), [typeof(int), typeof(int)]);
            if (moveMethod != null)
            {
                for (int i = 0; i < sortedItems.Count; i++)
                {
                    var currentIndex = list.IndexOf(sortedItems[i]);
                    if (currentIndex >= 0 && currentIndex != i)
                        moveMethod.Invoke(collection, [currentIndex, i]);
                }

                return;
            }

            if (list.IsFixedSize || list.IsReadOnly)
                throw new NotSupportedException("Collection sorting requires a writable list.");

            list.Clear();
            foreach (var item in sortedItems)
                list.Add(item);
        }

        private sealed class SortItem
        {
            public SortItem(object item, object? value, int index)
            {
                Item = item;
                Value = value;
                Index = index;
            }

            public object Item { get; }

            public object? Value { get; }

            public int Index { get; }
        }

        private sealed class SortItemComparer : IComparer<SortItem>
        {
            private readonly bool _descending;

            public SortItemComparer(bool descending)
            {
                _descending = descending;
            }

            public int Compare(SortItem? x, SortItem? y)
            {
                if (x == null && y == null)
                    return 0;
                if (x == null)
                    return -1;
                if (y == null)
                    return 1;

                int result = CompareValues(x.Value, y.Value);
                if (_descending)
                    result = -result;

                return result != 0 ? result : x.Index.CompareTo(y.Index);
            }
        }
    }
}
