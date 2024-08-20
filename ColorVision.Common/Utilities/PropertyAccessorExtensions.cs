using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Common.Utilities.Extensions
{
    internal static class PropertyAccessorExtensions
    {

        // 缓存Get方法的委托
        private static readonly Dictionary<string, Func<object, object>> GettersCache = new();

        // 缓存Set方法的委托
        private static readonly Dictionary<string, Action<object, object>> SettersCache = new();

        // GetValue 扩展方法
        public static object GetValue(this object obj, string propertyName)
        {
            var key = $"{obj.GetType().FullName}.{propertyName}";

            if (!GettersCache.TryGetValue(key, out var getter))
            {
                getter = CreateGetDelegate(obj.GetType(), propertyName);
                GettersCache[key] = getter;
            }

            return getter(obj);
        }

        // SetValue 扩展方法
        public static void SetValue(this object obj, string propertyName, object value)
        {
            var key = $"{obj.GetType().FullName}.{propertyName}";

            if (!SettersCache.TryGetValue(key, out var setter))
            {
                setter = CreateSetDelegate(obj.GetType(), propertyName);
                SettersCache[key] = setter;
            }

            setter(obj, value);
        }

        // 创建 Get 委托
        private static Func<object, object> CreateGetDelegate(Type type, string propertyName)
        {
            var parameter = Expression.Parameter(typeof(object), "obj");
            var instance = Expression.Convert(parameter, type);
            var property = Expression.Property(instance, propertyName);
            var convert = Expression.Convert(property, typeof(object));

            return Expression.Lambda<Func<object, object>>(convert, parameter).Compile();
        }

        // 创建 Set 委托
        private static Action<object, object> CreateSetDelegate(Type type, string propertyName)
        {
            var parameter = Expression.Parameter(typeof(object), "obj");
            var valueParameter = Expression.Parameter(typeof(object), "value");

            var instance = Expression.Convert(parameter, type);
            var property = Expression.Property(instance, propertyName);
            var convertValue = Expression.Convert(valueParameter, property.Type);

            var assign = Expression.Assign(property, convertValue);

            return Expression.Lambda<Action<object, object>>(assign, parameter, valueParameter).Compile();
        }

    }
}
