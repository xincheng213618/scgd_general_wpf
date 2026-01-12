using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ColorVision.UI.PropertyEditor.Json
{
    /// <summary>
    /// Simple wrapper for JObject that exposes properties through ICustomTypeDescriptor
    /// Treats JObject like a class with typed properties
    /// </summary>
    public class JObjectPropertyWrapper : INotifyPropertyChanged, ICustomTypeDescriptor
    {
        private readonly JObject _jObject;
        private readonly Dictionary<string, JObjectPropertyDescriptor> _descriptors;

        public event PropertyChangedEventHandler? PropertyChanged;

        public JObjectPropertyWrapper(JObject jObject)
        {
            _jObject = jObject ?? throw new ArgumentNullException(nameof(jObject));
            _descriptors = new Dictionary<string, JObjectPropertyDescriptor>();

            // Create descriptors for all properties
            foreach (var prop in jObject.Properties())
            {
                var descriptor = new JObjectPropertyDescriptor(prop.Name, _jObject);
                _descriptors[prop.Name] = descriptor;
            }
        }

        public JObject GetJObject() => _jObject;

        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region ICustomTypeDescriptor Implementation

        AttributeCollection ICustomTypeDescriptor.GetAttributes() => AttributeCollection.Empty;
        string? ICustomTypeDescriptor.GetClassName() => nameof(JObjectPropertyWrapper);
        string? ICustomTypeDescriptor.GetComponentName() => null;
        TypeConverter? ICustomTypeDescriptor.GetConverter() => null;
        EventDescriptor? ICustomTypeDescriptor.GetDefaultEvent() => null;
        PropertyDescriptor? ICustomTypeDescriptor.GetDefaultProperty() => null;
        object? ICustomTypeDescriptor.GetEditor(Type editorBaseType) => null;
        EventDescriptorCollection ICustomTypeDescriptor.GetEvents() => EventDescriptorCollection.Empty;
        EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[]? attributes) => EventDescriptorCollection.Empty;

        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties()
        {
            return new PropertyDescriptorCollection(_descriptors.Values.ToArray());
        }

        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[]? attributes)
        {
            return new PropertyDescriptorCollection(_descriptors.Values.ToArray());
        }

        object? ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor? pd) => this;

        #endregion

        /// <summary>
        /// PropertyDescriptor that works directly with JObject properties
        /// </summary>
        private class JObjectPropertyDescriptor : PropertyDescriptor
        {
            private readonly string _propertyName;
            private readonly JObject _jObject;
            private readonly Type _propertyType;

            public JObjectPropertyDescriptor(string propertyName, JObject jObject)
                : base(propertyName, new Attribute[]
                {
                    new CategoryAttribute("JSON Properties"),
                    new DisplayNameAttribute(FormatPropertyName(propertyName)),
                    new DescriptionAttribute($"JSON property: {propertyName}")
                })
            {
                _propertyName = propertyName;
                _jObject = jObject;
                _propertyType = InferNetType(_jObject[propertyName]);
            }

            public override Type ComponentType => typeof(JObjectPropertyWrapper);
            public override bool IsReadOnly => false;
            public override Type PropertyType => _propertyType;

            public override bool CanResetValue(object component) => false;
            public override void ResetValue(object component) { }
            public override bool ShouldSerializeValue(object component) => true;

            public override object? GetValue(object? component)
            {
                var token = _jObject[_propertyName];
                if (token == null || token.Type == JTokenType.Null)
                    return GetDefaultValue(_propertyType);

                return ConvertJTokenToTypedValue(token, _propertyType);
            }

            public override void SetValue(object? component, object? value)
            {
                if (value == null)
                {
                    _jObject[_propertyName] = JValue.CreateNull();
                }
                else
                {
                    _jObject[_propertyName] = JToken.FromObject(value);
                }

                if (component is JObjectPropertyWrapper wrapper)
                {
                    wrapper.OnPropertyChanged(_propertyName);
                }
            }

            /// <summary>
            /// Infers .NET type from JToken - maps to basic types that PropertyEditor understands
            /// </summary>
            private static Type InferNetType(JToken? token)
            {
                if (token == null || token.Type == JTokenType.Null)
                    return typeof(string);

                return token.Type switch
                {
                    JTokenType.Integer => typeof(int),      // Use int instead of long for better editor support
                    JTokenType.Float => typeof(double),
                    JTokenType.Boolean => typeof(bool),
                    JTokenType.String => typeof(string),
                    JTokenType.Date => typeof(DateTime),
                    _ => typeof(string) // Fallback to string for complex types
                };
            }

            /// <summary>
            /// Converts JToken to typed .NET value
            /// </summary>
            private static object? ConvertJTokenToTypedValue(JToken token, Type targetType)
            {
                try
                {
                    if (targetType == typeof(int))
                        return token.Value<int>();
                    if (targetType == typeof(long))
                        return token.Value<long>();
                    if (targetType == typeof(double))
                        return token.Value<double>();
                    if (targetType == typeof(float))
                        return token.Value<float>();
                    if (targetType == typeof(bool))
                        return token.Value<bool>();
                    if (targetType == typeof(string))
                        return token.Value<string>();
                    if (targetType == typeof(DateTime))
                        return token.Value<DateTime>();

                    // Fallback to string
                    return token.ToString();
                }
                catch
                {
                    return GetDefaultValue(targetType);
                }
            }

            /// <summary>
            /// Gets default value for a type
            /// </summary>
            private static object? GetDefaultValue(Type type)
            {
                if (type == typeof(string))
                    return string.Empty;
                if (type.IsValueType)
                    return Activator.CreateInstance(type);
                return null;
            }

            /// <summary>
            /// Formats property name for display (camelCase -> Title Case)
            /// </summary>
            private static string FormatPropertyName(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return name;

                // Convert camelCase to Title Case
                var result = new System.Text.StringBuilder();
                result.Append(char.ToUpper(name[0]));

                for (int i = 1; i < name.Length; i++)
                {
                    if (char.IsUpper(name[i]) && i > 0)
                        result.Append(' ');
                    result.Append(name[i]);
                }

                return result.ToString();
            }
        }
    }
}
