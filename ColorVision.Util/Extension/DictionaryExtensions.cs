using System.Collections.Generic;

namespace ColorVision.Extension
{
    /// <summary>
    /// 对字典的扩展(解决输出不规范的问题)
    /// </summary>
    public static class DictionaryExtensions
    {

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
