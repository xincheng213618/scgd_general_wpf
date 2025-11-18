using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.PropertyEditor.Editor.List
{
    public partial class ListItemEditorWindow : Window
    {
        private readonly Type _elementType;
        private readonly ValueWrapper _valueWrapper;

        public object? EditedValue => _valueWrapper.Value;

        // Wrapper class to hold the value as a property so we can use PropertyEditor system
        private class ValueWrapper : INotifyPropertyChanged
        {
            private object? _value;

            public object? Value
            {
                get => _value;
                set
                {
                    if (!Equals(_value, value))
                    {
                        _value = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                    }
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        public ListItemEditorWindow(Type elementType, object? initialValue)
        {
            InitializeComponent();
            _elementType = elementType;
            _valueWrapper = new ValueWrapper { Value = initialValue };

            CreateEditor();
        }

        private void CreateEditor()
        {
            // Get the base property from ValueWrapper
            var baseProperty = typeof(ValueWrapper).GetProperty(nameof(ValueWrapper.Value))!;
            
            // Try to get the appropriate editor type for the element type
            var editorType = DetermineEditorType(_elementType);
            
            if (editorType != null)
            {
                try
                {
                    var editor = PropertyEditorHelper.GetOrCreateEditor(editorType);
                    
                    // Create a custom PropertyInfo that returns the correct type
                    var customProperty = new CustomPropertyInfo(baseProperty, _elementType);
                    var dockPanel = editor.GenProperties(customProperty, _valueWrapper);
                    
                    EditorPanel.Children.Add(dockPanel);
                    return;
                }
                catch
                {
                    // Fall back to default behavior
                }
            }

            // Fallback: create a simple textbox editor
            CreateFallbackEditor();
        }

        private Type? DetermineEditorType(Type elementType)
        {
            // For strings, use TextSelectFilePropertiesEditor to get file/folder pickers
            if (elementType == typeof(string))
            {
                return typeof(TextSelectFilePropertiesEditor);
            }
            
            // Otherwise use the registered editor for the type
            return PropertyEditorHelper.GetEditorTypeForPropertyType(elementType);
        }

        private void CreateFallbackEditor()
        {
            var label = new TextBlock
            {
                Text = "值:",
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.Bold
            };
            EditorPanel.Children.Add(label);

            var textBox = new TextBox
            {
                Text = _valueWrapper.Value?.ToString() ?? string.Empty,
                Style = PropertyEditorHelper.TextBoxSmallStyle,
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            textBox.TextChanged += (s, e) =>
            {
                try
                {
                    _valueWrapper.Value = ConvertValue(textBox.Text, _elementType);
                }
                catch
                {
                    // Ignore conversion errors during typing
                }
            };

            EditorPanel.Children.Add(textBox);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static object? ConvertValue(string input, Type targetType)
        {
            if (targetType == typeof(string))
                return input;

            if (string.IsNullOrWhiteSpace(input))
            {
                if (targetType.IsValueType)
                    return Activator.CreateInstance(targetType);
                return null;
            }

            targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (targetType == typeof(int))
                return int.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(long))
                return long.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(short))
                return short.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(byte))
                return byte.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(uint))
                return uint.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(ulong))
                return ulong.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(ushort))
                return ushort.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(sbyte))
                return sbyte.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(float))
                return float.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(double))
                return double.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(decimal))
                return decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);

            return Convert.ChangeType(input, targetType, System.Globalization.CultureInfo.InvariantCulture);
        }

        // Custom PropertyInfo that overrides the PropertyType to return our element type
        private class CustomPropertyInfo : PropertyInfo
        {
            private readonly PropertyInfo _baseProperty;
            private readonly Type _customType;

            public CustomPropertyInfo(PropertyInfo baseProperty, Type customType)
            {
                _baseProperty = baseProperty;
                _customType = customType;
            }

            public override Type PropertyType => _customType;
            public override string Name => _baseProperty.Name;
            public override Type? DeclaringType => _baseProperty.DeclaringType;
            public override Type? ReflectedType => _baseProperty.ReflectedType;
            public override PropertyAttributes Attributes => _baseProperty.Attributes;
            public override bool CanRead => _baseProperty.CanRead;
            public override bool CanWrite => _baseProperty.CanWrite;

            public override object? GetValue(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? index, System.Globalization.CultureInfo? culture)
                => _baseProperty.GetValue(obj, invokeAttr, binder, index, culture);

            public override void SetValue(object? obj, object? value, BindingFlags invokeAttr, Binder? binder, object?[]? index, System.Globalization.CultureInfo? culture)
                => _baseProperty.SetValue(obj, value, invokeAttr, binder, index, culture);

            public override MethodInfo[] GetAccessors(bool nonPublic) => _baseProperty.GetAccessors(nonPublic);
            public override MethodInfo? GetGetMethod(bool nonPublic) => _baseProperty.GetGetMethod(nonPublic);
            public override MethodInfo? GetSetMethod(bool nonPublic) => _baseProperty.GetSetMethod(nonPublic);
            public override ParameterInfo[] GetIndexParameters() => _baseProperty.GetIndexParameters();
            public override object[] GetCustomAttributes(bool inherit) => _baseProperty.GetCustomAttributes(inherit);
            public override object[] GetCustomAttributes(Type attributeType, bool inherit) => _baseProperty.GetCustomAttributes(attributeType, inherit);
            public override bool IsDefined(Type attributeType, bool inherit) => _baseProperty.IsDefined(attributeType, inherit);
        }
    }
}
