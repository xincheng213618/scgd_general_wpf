using ColorVision.UI;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;

namespace System.ComponentModel
{
    internal static class CollectionTypeHelper
    {
        public static bool IsSupportedCollectionType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsArray)
                return type.GetArrayRank() == 1;

            if (!type.IsGenericType)
                return false;

            var genericTypeDef = type.GetGenericTypeDefinition();
            return genericTypeDef == typeof(List<>) ||
                   genericTypeDef == typeof(ObservableCollection<>) ||
                   genericTypeDef == typeof(Collection<>) ||
                   genericTypeDef == typeof(System.Collections.Generic.IList<>) ||
                   genericTypeDef == typeof(System.Collections.Generic.ICollection<>) ||
                   genericTypeDef == typeof(System.Collections.Generic.IEnumerable<>);
        }

        public static bool TryGetElementType(Type collectionType, out Type elementType)
        {
            collectionType = Nullable.GetUnderlyingType(collectionType) ?? collectionType;
            if (collectionType.IsArray)
            {
                elementType = collectionType.GetElementType()!;
                return elementType != null;
            }

            if (collectionType.IsGenericType)
            {
                elementType = collectionType.GetGenericArguments()[0];
                return true;
            }

            elementType = null!;
            return false;
        }

        public static bool IsSupportedElementType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (IsSimpleElementType(type))
                return true;

            if (IsSupportedCollectionType(type) && TryGetElementType(type, out var nestedElementType))
                return IsSupportedElementType(nestedElementType);

            if (PropertyEditorHelper.GetEditorTypeForPropertyType(type) != null)
                return true;

            if (type.IsValueType)
                return HasEditableMembers(type);

            if (!type.IsClass || type.IsAbstract || typeof(Delegate).IsAssignableFrom(type) ||
                typeof(DependencyObject).IsAssignableFrom(type) ||
                typeof(IDictionary).IsAssignableFrom(type))
            {
                return false;
            }

            return type.GetConstructor(Type.EmptyTypes) != null || HasEditableMembers(type);
        }

        public static bool CanUseListDialog(Type elementType)
        {
            elementType = Nullable.GetUnderlyingType(elementType) ?? elementType;
            if (IsSimpleElementType(elementType))
                return true;

            if (IsSupportedCollectionType(elementType))
                return true;

            if (PropertyEditorHelper.GetEditorTypeForPropertyType(elementType) != null)
                return true;

            return elementType.IsClass &&
                   !elementType.IsAbstract &&
                   typeof(Delegate).IsAssignableFrom(elementType) == false &&
                   typeof(DependencyObject).IsAssignableFrom(elementType) == false &&
                   typeof(IDictionary).IsAssignableFrom(elementType) == false;
        }

        public static IList? CreateEditableList(object collection, Type elementType)
        {
            if (collection is IList list && !list.IsFixedSize && !list.IsReadOnly)
                return list;

            if (collection is not IEnumerable enumerable)
                return null;

            var listType = typeof(List<>).MakeGenericType(elementType);
            var editableList = (IList)Activator.CreateInstance(listType)!;
            foreach (var item in enumerable)
                editableList.Add(item);

            return editableList;
        }

        public static object CreateEmptyCollection(Type targetType, Type elementType)
        {
            targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (targetType.IsArray)
                return Array.CreateInstance(elementType, 0);

            var genericTypeDef = targetType.GetGenericTypeDefinition();
            if (genericTypeDef == typeof(ObservableCollection<>))
                return Activator.CreateInstance(typeof(ObservableCollection<>).MakeGenericType(elementType))!;

            if (genericTypeDef == typeof(Collection<>))
                return Activator.CreateInstance(typeof(Collection<>).MakeGenericType(elementType))!;

            return Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        }

        public static object ConvertListToDeclaredType(Type targetType, IList sourceList, Type elementType)
        {
            targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (targetType.IsArray)
            {
                var array = Array.CreateInstance(elementType, sourceList.Count);
                sourceList.CopyTo(array, 0);
                return array;
            }

            if (sourceList.GetType() == targetType)
                return sourceList;

            var genericTypeDef = targetType.GetGenericTypeDefinition();
            if (genericTypeDef == typeof(ObservableCollection<>))
                return CopyToNewCollection(typeof(ObservableCollection<>).MakeGenericType(elementType), sourceList);

            if (genericTypeDef == typeof(Collection<>))
                return CopyToNewCollection(typeof(Collection<>).MakeGenericType(elementType), sourceList);

            return sourceList;
        }

        private static object CopyToNewCollection(Type collectionType, IList sourceList)
        {
            var collection = (IList)Activator.CreateInstance(collectionType)!;
            foreach (var item in sourceList)
                collection.Add(item);

            return collection;
        }

        private static bool IsSimpleElementType(Type type)
        {
            return type == typeof(byte) || type == typeof(sbyte) ||
                   type == typeof(short) || type == typeof(ushort) ||
                   type == typeof(int) || type == typeof(uint) ||
                   type == typeof(long) || type == typeof(ulong) ||
                   type == typeof(float) || type == typeof(double) ||
                   type == typeof(decimal) || type == typeof(string) ||
                   type == typeof(bool) || type.IsEnum;
        }

        private static bool HasEditableMembers(Type type)
        {
            return type.GetFields(BindingFlags.Public | BindingFlags.Instance).Length > 0 ||
                   type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                       .Any(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);
        }
    }
}
