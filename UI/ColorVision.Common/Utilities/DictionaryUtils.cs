using System.Collections.Generic;

namespace ColorVision.Common.Utilities
{
    /// <summary>
    /// 对字典的扩展(解决输出不规范的问题)
    /// </summary>
    public static class DictionaryUtils
    {
        public static T? GetValue<T>(this Dictionary<string, T> This, string key) where T:new()
        {

            if (This.TryGetValue(key, out T value) && value is T t)
            {
                return t;
            }
            T t1 = new T();
            This.Add(key, t1);
            return t1;
        }


        public static string GetString(this Dictionary<string, object> This, string key)
        {
            if (This.TryGetValue(key, out object value) && value is string str)
            {
                return str;
            }
            return string.Empty;
        }


        public static int? GetInt(this Dictionary<string, object> This, string key)
        {
            if (This.TryGetValue(key, out object value))
            {
                if (value is int i)
                    return i;
                else if (value is string str && int.TryParse(str, out int value2))
                    return value2;
                else
                    return null;
            }
            return null;
        }

        public static object? GetValue(this Dictionary<string, object> This, string key)=> This.TryGetValue(key, out object value) ? value : null;
    }
}
