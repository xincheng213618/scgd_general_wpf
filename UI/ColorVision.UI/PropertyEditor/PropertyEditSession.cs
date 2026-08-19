using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.UI
{
    /// <summary>
    /// Controls whether a property editor writes directly to its source or commits a working copy.
    /// </summary>
    public enum PropertyEditorEditMode
    {
        Immediate,
        Transactional
    }

    /// <summary>
    /// Owns the editable object used by a property editor and provides explicit commit/reset behavior.
    /// </summary>
    public sealed class PropertyEditSession
    {
        private readonly object _initialSnapshot;

        public object Source { get; }
        public object EditableObject { get; }
        public PropertyEditorEditMode Mode { get; }

        public bool IsTransactional => Mode == PropertyEditorEditMode.Transactional;

        private PropertyEditSession(object source, PropertyEditorEditMode mode)
        {
            ArgumentNullException.ThrowIfNull(source);

            Source = source;
            Mode = mode;
            _initialSnapshot = PropertyGraphCopy.Clone(source);
            EditableObject = mode == PropertyEditorEditMode.Transactional
                ? PropertyGraphCopy.Clone(_initialSnapshot)
                : source;
        }

        public static PropertyEditSession Create(object source, PropertyEditorEditMode mode)
            => new(source, mode);

        public void Commit()
        {
            if (IsTransactional)
                PropertyGraphCopy.Copy(EditableObject, Source);
        }

        public void Reset()
        {
            PropertyGraphCopy.Copy(_initialSnapshot, EditableObject);
        }

        public void ResetToDefaults()
        {
            Type sourceType = Source.GetType();
            object defaults = PropertyGraphCopy.CreateObject(sourceType);
            PropertyGraphCopy.Copy(defaults, EditableObject);
        }
    }

    internal static class PropertyGraphCopy
    {
        public static object Clone(object source)
        {
            ArgumentNullException.ThrowIfNull(source);
            return CloneValue(source, source.GetType(), new Dictionary<object, object>(ReferenceEqualityComparer.Instance))!;
        }

        public static void Copy(object source, object target)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);

            Type sourceType = source.GetType();
            if (sourceType != target.GetType())
                throw new ArgumentException($"Source type '{sourceType.FullName}' does not match target type '{target.GetType().FullName}'.", nameof(target));

            var visited = new Dictionary<object, object>(ReferenceEqualityComparer.Instance)
            {
                [source] = target
            };
            CopyProperties(source, target, sourceType, visited);
        }

        public static object CreateObject(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            try
            {
                return Activator.CreateInstance(type, nonPublic: true)
                    ?? throw new InvalidOperationException($"Could not create an instance of '{type.FullName}'.");
            }
            catch (Exception ex) when (ex is MissingMethodException or MemberAccessException or TargetInvocationException)
            {
                throw new InvalidOperationException($"Type '{type.FullName}' must be constructible to support transactional property editing.", ex);
            }
        }

        private static object? CloneValue(object? value, Type declaredType, Dictionary<object, object> visited)
        {
            if (value == null)
                return null;

            Type runtimeType = value.GetType();
            if (IsImmutable(runtimeType) || value is Delegate || value is ICommand || value is Type)
                return value;

            if (!runtimeType.IsValueType && visited.TryGetValue(value, out object? existing))
                return existing;

            if (value is Freezable freezable)
                return freezable.CloneCurrentValue();

            // WPF runtime objects are bound to their creating dispatcher and often contain
            // framework-managed state (for example DataTemplate.TemplateContent) that cannot
            // be reconstructed through reflection. They are runtime references, not owned
            // configuration data, so keep the reference while cloning the surrounding graph.
            if (value is DispatcherObject)
                return value;

            if (value is Array array)
                return CloneArray(array, visited);

            if (value is IDictionary dictionary)
                return CloneDictionary(dictionary, runtimeType, declaredType, visited);

            if (value is IList list)
                return CloneList(list, runtimeType, declaredType, visited);

            if (runtimeType.IsValueType)
                return value;

            TypeConverter converter = TypeDescriptor.GetConverter(runtimeType);
            if (converter.CanConvertTo(typeof(string)) && converter.CanConvertFrom(typeof(string)))
            {
                string? text = converter.ConvertToInvariantString(value);
                if (text != null)
                    return converter.ConvertFromInvariantString(text);
            }

            object clone;
            try
            {
                clone = CreateObject(runtimeType);
            }
            catch (InvalidOperationException) when (value is ICloneable cloneable)
            {
                return cloneable.Clone();
            }

            visited[value] = clone;
            CopyProperties(value, clone, runtimeType, visited);
            return clone;
        }

        private static Array CloneArray(Array source, Dictionary<object, object> visited)
        {
            if (source.Rank != 1)
                throw new NotSupportedException($"Transactional property editing does not support {source.Rank}-dimensional arrays.");

            Type elementType = source.GetType().GetElementType()!;
            var clone = Array.CreateInstance(elementType, source.Length);
            visited[source] = clone;
            for (int index = 0; index < source.Length; index++)
                clone.SetValue(CloneValue(source.GetValue(index), elementType, visited), index);
            return clone;
        }

        private static object CloneDictionary(IDictionary source, Type runtimeType, Type declaredType, Dictionary<object, object> visited)
        {
            object cloneObject = TryCreateCollection(runtimeType)
                ?? CreateDictionaryFallback(runtimeType, declaredType);
            if (cloneObject is not IDictionary clone)
                throw new NotSupportedException($"Dictionary type '{runtimeType.FullName}' cannot be cloned for transactional property editing.");

            visited[source] = clone;
            Type[] genericArguments = GetDictionaryTypes(runtimeType, declaredType);
            Type keyType = genericArguments[0];
            Type valueType = genericArguments[1];
            foreach (DictionaryEntry entry in source)
            {
                object clonedKey = CloneValue(entry.Key, keyType, visited)
                    ?? throw new NotSupportedException($"Dictionary type '{runtimeType.FullName}' produced a null key while cloning.");
                clone.Add(clonedKey, CloneValue(entry.Value, valueType, visited));
            }
            return clone;
        }

        private static object CloneList(IList source, Type runtimeType, Type declaredType, Dictionary<object, object> visited)
        {
            object cloneObject = TryCreateCollection(runtimeType)
                ?? CreateListFallback(runtimeType, declaredType);
            if (cloneObject is not IList clone || clone.IsReadOnly || clone.IsFixedSize)
                throw new NotSupportedException($"List type '{runtimeType.FullName}' cannot be cloned for transactional property editing.");

            visited[source] = clone;
            Type elementType = GetCollectionElementType(runtimeType, declaredType) ?? typeof(object);
            foreach (object? item in source)
                clone.Add(CloneValue(item, elementType, visited));
            return clone;
        }

        private static void CopyProperties(object source, object target, Type type, Dictionary<object, object> visited)
        {
            foreach (PropertyInfo property in GetCopyableProperties(type))
            {
                object? sourceValue;
                try
                {
                    sourceValue = property.GetValue(source);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Could not read property '{type.FullName}.{property.Name}' while creating a property edit session.", ex);
                }

                object? clonedValue = CloneValue(sourceValue, property.PropertyType, visited);
                try
                {
                    property.SetValue(target, clonedValue);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Could not write property '{type.FullName}.{property.Name}' while creating a property edit session.", ex);
                }
            }
        }

        private static IEnumerable<PropertyInfo> GetCopyableProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.CanRead
                    && property.CanWrite
                    && property.GetIndexParameters().Length == 0
                    && (property.GetCustomAttribute<BrowsableAttribute>()?.Browsable ?? true)
                    && !typeof(ICommand).IsAssignableFrom(property.PropertyType));
        }

        private static object? TryCreateCollection(Type type)
        {
            if (type.IsAbstract || type.IsInterface)
                return null;

            try
            {
                return Activator.CreateInstance(type, nonPublic: true);
            }
            catch
            {
                return null;
            }
        }

        private static object CreateListFallback(Type runtimeType, Type declaredType)
        {
            Type elementType = GetCollectionElementType(runtimeType, declaredType) ?? typeof(object);
            Type listType = typeof(List<>).MakeGenericType(elementType);
            if (!declaredType.IsAssignableFrom(listType) && !runtimeType.IsAssignableFrom(listType))
                throw new NotSupportedException($"List type '{runtimeType.FullName}' has no constructible transactional representation.");
            return Activator.CreateInstance(listType)!;
        }

        private static object CreateDictionaryFallback(Type runtimeType, Type declaredType)
        {
            Type[] arguments = GetDictionaryTypes(runtimeType, declaredType);
            Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(arguments);
            if (!declaredType.IsAssignableFrom(dictionaryType) && !runtimeType.IsAssignableFrom(dictionaryType))
                throw new NotSupportedException($"Dictionary type '{runtimeType.FullName}' has no constructible transactional representation.");
            return Activator.CreateInstance(dictionaryType)!;
        }

        private static Type? GetCollectionElementType(Type runtimeType, Type declaredType)
        {
            return FindGenericInterface(runtimeType, typeof(IEnumerable<>))?.GetGenericArguments()[0]
                ?? FindGenericInterface(declaredType, typeof(IEnumerable<>))?.GetGenericArguments()[0];
        }

        private static Type[] GetDictionaryTypes(Type runtimeType, Type declaredType)
        {
            Type? dictionaryInterface = FindGenericInterface(runtimeType, typeof(IDictionary<,>))
                ?? FindGenericInterface(declaredType, typeof(IDictionary<,>));
            return dictionaryInterface?.GetGenericArguments() ?? new[] { typeof(object), typeof(object) };
        }

        private static Type? FindGenericInterface(Type type, Type genericDefinition)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == genericDefinition)
                return type;
            return type.GetInterfaces().FirstOrDefault(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == genericDefinition);
        }

        private static bool IsImmutable(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsPrimitive
                || type.IsEnum
                || type == typeof(string)
                || type == typeof(decimal)
                || type == typeof(DateTime)
                || type == typeof(DateTimeOffset)
                || type == typeof(TimeSpan)
                || type == typeof(Guid)
                || type == typeof(Uri)
                || type == typeof(Version);
        }
    }
}
