using System;
using System.Linq;
using System.Reflection;

namespace ColorVision.MVVM
{
    /// <summary>
    /// ViewMode的扩展
    /// </summary>
    public static class ViewModeBaseExtensions
    {
        //复制一个新的对象
        public static T CopyTo<T>(this T source) where T : ViewModelBase, new()
        {
            T target = new T();
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

        //复制一个新的对象
        public static void CopyTo<T>(this T source, T target) where T:ViewModelBase
        {
            Type type = source.GetType();
            var fields = type.GetRuntimeFields().ToList();
            foreach (var field in fields)
            {
                try
                {
                    field.SetValue(target, field.GetValue(source));
                }
                catch
                {

                }
            }

            var properties = type.GetRuntimeProperties().ToList();
            foreach (var property in properties)
            {
                try
                {
                    property.SetValue(target, property.GetValue(source));
                }
                catch
                {

                }
            }
        }

    }
}
