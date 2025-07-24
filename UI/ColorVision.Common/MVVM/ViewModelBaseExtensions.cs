
using System;
using System.Linq;
using System.Reflection;
using System.Windows.Input;

namespace ColorVision.Common.MVVM
{
    /// <summary>
    /// ViewMode的扩展
    /// </summary>
    public static class ViewModeBaseExtensions
    {
        //复制一个新的对象
        public static T Clone<T>(this T source) where T : new()
        {
            Type type = source.GetType();
            if (Activator.CreateInstance(type) is T target)
            {
                source.CopyTo(target);
            }
            else
            {
                target = new T();
                source.CopyTo(target);
            }
            return target;
        }

        public static bool EqualMax<T>(this T source, T target) where T :new()
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);

            Type type = source.GetType();

            // 可能需要检查source和target是否是同一个类型或者target是否是source的子类。
            if (!type.IsAssignableFrom(target.GetType()))
            {
                return false;
            }

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                  .Where(p => p.CanRead && p.CanWrite);

            foreach (var property in properties)
            {
                var value1 = property.GetValue(source);
                var value2 = property.GetValue(target);

                if (!object.Equals(value1, value2))
                {
                    return false;
                }
            }
            return true;
        }

        public static void Reset<T>(this T source) where T : new()
        {
            ArgumentNullException.ThrowIfNull(source);

            Type type = source.GetType();

            // Create a new instance of the same type as source
            if (Activator.CreateInstance(type) is T newInstance)
            {
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                      .Where(p => p.CanRead && p.CanWrite);

                foreach (var property in properties)
                {
                    // Copy properties from the new instance to the source
                    var value = property.GetValue(newInstance);
                    property.SetValue(source, value);
                }
            }
        }



        public static T DeepCopy<T>(this T source) where T : new()
        {
            #pragma warning disable SYSLIB0011
            using var ms = new System.IO.MemoryStream();
            var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            formatter.Serialize(ms, source);
            ms.Position = 0;
            return (T)formatter.Deserialize(ms);
            #pragma warning restore SYSLIB0011
        }

        public static void CopyToSimple<T>(this T source, T target)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);

            Type type = source.GetType();

            // 可能需要检查source和target是否是同一个类型或者target是否是source的子类。
            if (!type.IsAssignableFrom(target.GetType()))
            {
                throw new ArgumentException("Target must be the same type or a subtype of the source.");
            }

            // Copy fields and properties from the type and all its base types
            while (type != null && type != typeof(object))
            {
                // Copy fields
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (!field.IsInitOnly) // Ignore readonly fields
                    {
                        try
                        {
                            var fieldValue = field.GetValue(source);
                            if (!field.FieldType.IsClass)
                                field.SetValue(target, fieldValue);
                        }
                        catch (Exception ex)
                        {
                            // Handle or log the exception
                            Console.WriteLine($"Error copying field {field.Name}: {ex.Message}");
                        }
                    }
                }

                // Copy properties
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                     .Where(p => p.CanRead && p.CanWrite);
                foreach (var property in properties)
                {
                    try
                    {
                        var propertyValue = property.GetValue(source);
                        if (!property.PropertyType.IsClass)
                            property.SetValue(target, propertyValue);
                    }
                    catch (Exception ex)
                    {
                        // Handle or log the exception
                        Console.WriteLine($"Error copying property {property.Name}: {ex.Message}");
                    }
                }

                // Move to the base type
                type = type.BaseType;
            }
        }



        public static void CopyFrom<T>(this T source, T target) => target.CopyTo(source);
        public static void CopyTo<T>(this T source, T target) 
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);

            Type type = source.GetType();

            // 可能需要检查source和target是否是同一个类型或者target是否是source的子类。
            if (!type.IsAssignableFrom(target.GetType()))
            {
                throw new ArgumentException("Target must be the same type or a subtype of the source.");
            }

            // Copy fields and properties from the type and all its base types
            while (type != null && type != typeof(object))
            {
                // Copy fields
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (var field in fields)
                {
                    if (typeof(ICommand).IsAssignableFrom(field.FieldType))
                    {
                        // 不拷贝 Command
                        continue;
                    }
                    if (!field.IsInitOnly) // Ignore readonly fields
                    {
                        try
                        {
                            var fieldValue = field.GetValue(source);
                            if (fieldValue != null && field.FieldType.IsSubclassOf(typeof(ViewModelBase)))
                            {
                                if (fieldValue is ViewModelBase viewModelBase && Activator.CreateInstance(field.FieldType) is ViewModelBase targetFieldValue)
                                {
                                    viewModelBase.CopyTo(targetFieldValue);
                                    field.SetValue(target, targetFieldValue);
                                }
                            }
                            else
                            {
                                field.SetValue(target, fieldValue);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Handle or log the exception
                            Console.WriteLine($"Error copying field {field.Name}: {ex.Message}");
                        }
                    }
                }



                // Copy properties
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                     .Where(p => p.CanRead && p.CanWrite);
                foreach (var property in properties)
                {
                    if (typeof(ICommand).IsAssignableFrom(property.PropertyType))
                    {
                        // 不拷贝 Command
                        continue;
                    }
                    try
                    {
                        var propertyValue = property.GetValue(source);
                        if (propertyValue != null && property.PropertyType.IsSubclassOf(typeof(ViewModelBase)))
                        {
                            if (propertyValue is ViewModelBase viewModelBase && Activator.CreateInstance(property.PropertyType) is ViewModelBase targetPropertyValue)
                            {
                                viewModelBase.CopyTo(targetPropertyValue);
                                property.SetValue(target, targetPropertyValue);
                            }
                        }
                        else
                        {
                            property.SetValue(target, propertyValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Handle or log the exception
                        Console.WriteLine($"Error copying property {property.Name}: {ex.Message}");
                    }
                }

                // Move to the base type
                type = type.BaseType;
            }
        }

    }
}
