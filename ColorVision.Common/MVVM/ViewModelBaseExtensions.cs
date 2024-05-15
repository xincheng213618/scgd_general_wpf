using System;
using System.Linq;
using System.Reflection;

namespace ColorVision.Common.MVVM
{
    /// <summary>
    /// ViewMode的扩展
    /// </summary>
    public static class ViewModeBaseExtensions
    {
        //复制一个新的对象
        public static T Clone<T>(this T source) where T : ViewModelBase, new()
        {
            T target = new();
            source.CopyTo(target);
            return target;
        }

        public static T DeepCopy<T>(this T source) where T : ViewModelBase, new()
        {
            #pragma warning disable SYSLIB0011
            using var ms = new System.IO.MemoryStream();
            var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            formatter.Serialize(ms, source);
            ms.Position = 0;
            return (T)formatter.Deserialize(ms);
            #pragma warning restore SYSLIB0011
        }

        public static void CopyFrom<T>(this T source, T target) where T : ViewModelBase => target.CopyTo(source);

        //复制一个新的对象
        public static void CopyTo<T>(this T source, T target) where T:ViewModelBase
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);

            Type type = source.GetType();

            // 可能需要检查source和target是否是同一个类型或者target是否是source的子类。

            // Copy fields
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (!field.IsInitOnly) // Ignore readonly fields
                {
                    try
                    {
                        field.SetValue(target, field.GetValue(source));
                    }
                    catch (Exception ex)
                    {
                        // Handle or log the exception
                        Console.WriteLine($"Error copying field {field.Name}: {ex.Message}");
                    }
                }
            }

            // Copy properties
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite);
            foreach (var property in properties)
            {
                try
                {
                    property.SetValue(target, property.GetValue(source));
                }
                catch (Exception ex)
                {
                    // Handle or log the exception
                    Console.WriteLine($"Error copying property {property.Name}: {ex.Message}");
                }
            }
        }

    }
}
