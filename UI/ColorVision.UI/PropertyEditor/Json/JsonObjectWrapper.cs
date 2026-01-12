using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.PropertyEditor.Json
{
    /// <summary>
    /// Wrapper class for dynamic JSON objects that provides WPF property binding support
    /// </summary>
    public class JsonObjectWrapper : INotifyPropertyChanged, ICustomTypeDescriptor
    {
        private readonly Dictionary<string, object?> _values = new Dictionary<string, object?>();
        private readonly Dictionary<string, PropertyDescriptor> _propertyDescriptors = new Dictionary<string, PropertyDescriptor>();
        private readonly List<PropertyInfo> _properties = new List<PropertyInfo>();

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Adds a property to the dynamic object
        /// </summary>
        public void AddProperty(string name, Type type, object? value, string category = "General", string? displayName = null, string? description = null)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Property name cannot be null or empty", nameof(name));

            // Store the value
            _values[name] = value;

            // Create a custom property descriptor with attributes
            var descriptor = new JsonPropertyDescriptor(
                name: name,
                propertyType: type,
                category: category,
                displayName: displayName ?? name,
                description: description
            );

            _propertyDescriptors[name] = descriptor;

            // Create a PropertyInfo wrapper for reflection
            var propertyInfo = new DynamicPropertyInfo(name, type, this);
            _properties.Add(propertyInfo);
        }

        /// <summary>
        /// Gets the value of a property
        /// </summary>
        public object? GetValue(string propertyName)
        {
            return _values.TryGetValue(propertyName, out var value) ? value : null;
        }

        /// <summary>
        /// Sets the value of a property
        /// </summary>
        public void SetValue(string propertyName, object? value)
        {
            if (_values.ContainsKey(propertyName))
            {
                _values[propertyName] = value;
                OnPropertyChanged(propertyName);
            }
        }

        /// <summary>
        /// Gets all properties
        /// </summary>
        public IEnumerable<PropertyInfo> GetProperties()
        {
            return _properties;
        }

        /// <summary>
        /// Gets all property descriptors
        /// </summary>
        public IEnumerable<PropertyDescriptor> GetPropertyDescriptors()
        {
            return _propertyDescriptors.Values;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Custom PropertyDescriptor for dynamic properties with attributes
        /// </summary>
        private class JsonPropertyDescriptor : PropertyDescriptor
        {
            private readonly Type _propertyType;
            private readonly string _category;
            private readonly string _displayName;
            private readonly string? _description;

            public JsonPropertyDescriptor(string name, Type propertyType, string category, string displayName, string? description)
                : base(name, null)
            {
                _propertyType = propertyType;
                _category = category;
                _displayName = displayName;
                _description = description;
            }

            public override Type ComponentType => typeof(JsonObjectWrapper);
            public override bool IsReadOnly => false;
            public override Type PropertyType => _propertyType;
            public override string Category => _category;
            public override string DisplayName => _displayName;
            public override string Description => _description ?? string.Empty;

            public override bool CanResetValue(object component) => false;
            public override void ResetValue(object component) { }
            public override bool ShouldSerializeValue(object component) => false;

            public override object? GetValue(object? component)
            {
                if (component is JsonObjectWrapper wrapper)
                    return wrapper.GetValue(Name);
                return null;
            }

            public override void SetValue(object? component, object? value)
            {
                if (component is JsonObjectWrapper wrapper)
                    wrapper.SetValue(Name, value);
            }
        }

        /// <summary>
        /// Dynamic PropertyInfo implementation for reflection-based access
        /// </summary>
        private class DynamicPropertyInfo : PropertyInfo
        {
            private readonly string _name;
            private readonly Type _propertyType;
            private readonly JsonObjectWrapper _owner;

            public DynamicPropertyInfo(string name, Type propertyType, JsonObjectWrapper owner)
            {
                _name = name;
                _propertyType = propertyType;
                _owner = owner;
            }

            public override string Name => _name;
            public override Type PropertyType => _propertyType;
            public override PropertyAttributes Attributes => PropertyAttributes.None;
            public override bool CanRead => true;
            public override bool CanWrite => true;
            public override Type DeclaringType => typeof(JsonObjectWrapper);
            public override Type ReflectedType => typeof(JsonObjectWrapper);

            public override object? GetValue(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? index, System.Globalization.CultureInfo? culture)
            {
                if (obj is JsonObjectWrapper wrapper)
                    return wrapper.GetValue(_name);
                return null;
            }

            public override void SetValue(object? obj, object? value, BindingFlags invokeAttr, Binder? binder, object?[]? index, System.Globalization.CultureInfo? culture)
            {
                if (obj is JsonObjectWrapper wrapper)
                    wrapper.SetValue(_name, value);
            }

            public override MethodInfo[] GetAccessors(bool nonPublic) => Array.Empty<MethodInfo>();
            public override MethodInfo? GetGetMethod(bool nonPublic) => null;
            public override MethodInfo? GetSetMethod(bool nonPublic) => null;
            public override ParameterInfo[] GetIndexParameters() => Array.Empty<ParameterInfo>();

            public override object[] GetCustomAttributes(bool inherit)
            {
                // Return attributes for PropertyEditor
                var descriptor = _owner._propertyDescriptors[_name];
                return new object[]
                {
                    new CategoryAttribute(descriptor.Category),
                    new DisplayNameAttribute(descriptor.DisplayName),
                    new DescriptionAttribute(descriptor.Description)
                };
            }

            public override object[] GetCustomAttributes(Type attributeType, bool inherit)
            {
                var allAttributes = GetCustomAttributes(inherit);
                return allAttributes.Where(a => attributeType.IsInstanceOfType(a)).ToArray();
            }

            public override bool IsDefined(Type attributeType, bool inherit)
            {
                return GetCustomAttributes(attributeType, inherit).Length > 0;
            }
        }

        #region ICustomTypeDescriptor Implementation

        AttributeCollection ICustomTypeDescriptor.GetAttributes()
        {
            return AttributeCollection.Empty;
        }

        string? ICustomTypeDescriptor.GetClassName()
        {
            return nameof(JsonObjectWrapper);
        }

        string? ICustomTypeDescriptor.GetComponentName()
        {
            return null;
        }

        TypeConverter? ICustomTypeDescriptor.GetConverter()
        {
            return null;
        }

        EventDescriptor? ICustomTypeDescriptor.GetDefaultEvent()
        {
            return null;
        }

        PropertyDescriptor? ICustomTypeDescriptor.GetDefaultProperty()
        {
            return null;
        }

        object? ICustomTypeDescriptor.GetEditor(Type editorBaseType)
        {
            return null;
        }

        EventDescriptorCollection ICustomTypeDescriptor.GetEvents()
        {
            return EventDescriptorCollection.Empty;
        }

        EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[]? attributes)
        {
            return EventDescriptorCollection.Empty;
        }

        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties()
        {
            return new PropertyDescriptorCollection(_propertyDescriptors.Values.ToArray());
        }

        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[]? attributes)
        {
            return new PropertyDescriptorCollection(_propertyDescriptors.Values.ToArray());
        }

        object? ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor? pd)
        {
            return this;
        }

        #endregion
    }
}
