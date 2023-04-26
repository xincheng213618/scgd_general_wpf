using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Extension
{
    /// <summary>
    /// 对字典的扩展(解决输出不规范的问题)
    /// </summary>
    public static class DictionaryExtensions
    {

        public static string GetString(this Dictionary<string, object> This, string key)
        {
            if (This.ContainsKey(key) && This[key] is string value)
            {
                return value;
            }
            return string.Empty;
        }


        public static int? GetInt(this Dictionary<string, object> This, string key)
        {
            if (This.ContainsKey(key))
            {
                if (This[key] is int value)
                    return value;
                else if (This[key] is string value1 && int.TryParse(value1, out int value2))
                    return value2;
                else
                    return null;
            }
            return null;
        }

        public static double? GetDouble(this Dictionary<string, object> This, string key)
        {
            if (This.ContainsKey(key))
            {
                if (This[key] is double value)
                    return value;
                else if (This[key] is string value1 && double.TryParse(value1, out double value2))
                    return value2;
                else
                    return null;
            }
            return null;
        }

        public static object? GetValue(this Dictionary<string, object> This, string key)=> This.ContainsKey(key) ? This[key] : null;
    }
}
