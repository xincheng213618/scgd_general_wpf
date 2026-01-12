using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ColorVision.UI.PropertyEditor.Json
{
    /// <summary>
    /// Converts between JObject and dynamically typed objects for PropertyEditor
    /// </summary>
    public static class JsonPropertyEditorConverter
    {
        /// <summary>
        /// Converts a JObject to a dynamic object that can be used with PropertyEditor
        /// </summary>
        public static object ToObject(JObject jObject)
        {
            if (jObject == null)
                throw new ArgumentNullException(nameof(jObject));

            var wrapper = new JsonObjectWrapper();
            
            foreach (var prop in jObject.Properties())
            {
                var type = InferType(prop.Value);
                var value = ConvertJTokenToValue(prop.Value, type);
                
                // Add property with appropriate attributes
                wrapper.AddProperty(
                    name: prop.Name,
                    type: type,
                    value: value,
                    category: "JSON Properties",
                    displayName: FormatPropertyName(prop.Name),
                    description: $"Type: {GetTypeDescription(prop.Value)}"
                );
            }
            
            return wrapper;
        }

        /// <summary>
        /// Converts a dynamic object back to JObject
        /// </summary>
        public static JObject ToJObject(object obj)
        {
            if (obj is JsonObjectWrapper wrapper)
            {
                var jObject = new JObject();
                
                foreach (var property in wrapper.GetProperties())
                {
                    var value = property.GetValue(obj);
                    jObject[property.Name] = ConvertValueToJToken(value);
                }
                
                return jObject;
            }
            
            throw new ArgumentException("Object must be a JsonObjectWrapper", nameof(obj));
        }

        /// <summary>
        /// Infers the .NET type from a JToken
        /// </summary>
        private static Type InferType(JToken token)
        {
            return token.Type switch
            {
                JTokenType.Integer => typeof(long),
                JTokenType.Float => typeof(double),
                JTokenType.Boolean => typeof(bool),
                JTokenType.String => typeof(string),
                JTokenType.Date => typeof(DateTime),
                JTokenType.Null => typeof(string), // Treat null as string
                JTokenType.Object => typeof(JsonObjectWrapper), // Nested object
                JTokenType.Array => InferArrayType(token as JArray),
                _ => typeof(string)
            };
        }

        /// <summary>
        /// Infers the type of array elements
        /// </summary>
        private static Type InferArrayType(JArray? jArray)
        {
            if (jArray == null || jArray.Count == 0)
                return typeof(List<string>);

            // Get the type of the first non-null element
            var firstElement = jArray.FirstOrDefault(t => t.Type != JTokenType.Null);
            if (firstElement == null)
                return typeof(List<string>);

            var elementType = InferType(firstElement);
            
            // Create List<T> type
            return typeof(List<>).MakeGenericType(elementType);
        }

        /// <summary>
        /// Converts a JToken to a .NET value
        /// </summary>
        private static object? ConvertJTokenToValue(JToken token, Type targetType)
        {
            try
            {
                if (token.Type == JTokenType.Null)
                    return GetDefaultValue(targetType);

                if (token.Type == JTokenType.Object && token is JObject jObj)
                {
                    // Recursively convert nested objects
                    return ToObject(jObj);
                }

                if (token.Type == JTokenType.Array && token is JArray jArray)
                {
                    // Convert array
                    return ConvertJArray(jArray, targetType);
                }

                // Direct conversion
                return token.ToObject(targetType);
            }
            catch
            {
                return GetDefaultValue(targetType);
            }
        }

        /// <summary>
        /// Converts JArray to appropriate list type
        /// </summary>
        private static object ConvertJArray(JArray jArray, Type targetType)
        {
            if (!targetType.IsGenericType)
                return jArray.ToObject<List<object>>() ?? new List<object>();

            var elementType = targetType.GetGenericArguments()[0];
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (System.Collections.IList)(Activator.CreateInstance(listType) ?? throw new InvalidOperationException());

            foreach (var item in jArray)
            {
                var value = ConvertJTokenToValue(item, elementType);
                if (value != null)
                    list.Add(value);
            }

            return list;
        }

        /// <summary>
        /// Converts a .NET value back to JToken
        /// </summary>
        private static JToken ConvertValueToJToken(object? value)
        {
            if (value == null)
                return JValue.CreateNull();

            if (value is JsonObjectWrapper wrapper)
            {
                return ToJObject(wrapper);
            }

            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var jArray = new JArray();
                foreach (var item in enumerable)
                {
                    jArray.Add(ConvertValueToJToken(item));
                }
                return jArray;
            }

            return JToken.FromObject(value);
        }

        /// <summary>
        /// Gets the default value for a type
        /// </summary>
        private static object? GetDefaultValue(Type type)
        {
            if (type.IsValueType)
                return Activator.CreateInstance(type);
            return null;
        }

        /// <summary>
        /// Formats property name for display (e.g., "userName" -> "User Name")
        /// </summary>
        private static string FormatPropertyName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            // Insert space before capital letters (camelCase to Title Case)
            var result = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            
            // Capitalize first letter
            if (result.Length > 0)
                result = char.ToUpper(result[0]) + result.Substring(1);

            return result;
        }

        /// <summary>
        /// Gets a human-readable description of the JToken type
        /// </summary>
        private static string GetTypeDescription(JToken token)
        {
            return token.Type switch
            {
                JTokenType.Integer => "Integer",
                JTokenType.Float => "Decimal",
                JTokenType.Boolean => "Boolean",
                JTokenType.String => "Text",
                JTokenType.Date => "Date/Time",
                JTokenType.Object => "Object",
                JTokenType.Array => "Array",
                JTokenType.Null => "Null",
                _ => "Unknown"
            };
        }
    }
}
