using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using ColorVision.UI.PropertyEditor.Editor.List;

namespace ColorVision.UI.PropertyEditor.Editor.Dictionary
{
    public partial class DictionaryItemEditorWindow : Window
    {
        private readonly Type _keyType;
        private readonly Type _valueType;
        private readonly KeyValueWrapper _wrapper;
        private readonly ICollection _existingKeys;

        public object? EditedKey => _wrapper.Key;
        public object? EditedValue => _wrapper.Value;

        // Wrapper class to hold key and value as properties
        private sealed class KeyValueWrapper : INotifyPropertyChanged
        {
            private object? _key;
            private object? _value;

            public object? Key
            {
                get => _key;
                set
                {
                    if (!Equals(_key, value))
                    {
                        _key = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Key)));
                    }
                }
            }

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

        public DictionaryItemEditorWindow(Type keyType, Type valueType, object? initialKey, object? initialValue, ICollection existingKeys)
        {
            InitializeComponent();
            _keyType = keyType;
            _valueType = valueType;
            _existingKeys = existingKeys;
            _wrapper = new KeyValueWrapper 
            { 
                Key = initialKey,
                Value = initialValue
            };

            CreateEditors();
        }

        private void CreateEditors()
        {
            // Create editor for Key
            CreateKeyEditor();
            
            // Create editor for Value
            CreateValueEditor();
        }

        private void CreateKeyEditor()
        {
            // For key types that are classes (excluding string), use PropertyEditor
            if (IsEditableClass(_keyType))
            {
                CreateClassEditor(KeyEditorPanel, _wrapper.Key, _keyType, "Key");
                return;
            }
            
            // For nested lists
            if (IsGenericList(_keyType))
            {
                CreateNestedListEditor(KeyEditorPanel, _keyType, "Key");
                return;
            }

            var keyProperty = typeof(KeyValueWrapper).GetProperty(nameof(KeyValueWrapper.Key))!;
            var keyEditor = CreateEditorForType(_keyType, keyProperty, _wrapper);
            if (keyEditor != null)
            {
                KeyEditorPanel.Children.Add(keyEditor);
            }
            else
            {
                // Fallback: simple textbox
                var keyTextBox = new TextBox { Margin = new Thickness(5) };
                keyTextBox.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(KeyValueWrapper.Key))
                {
                    Source = _wrapper,
                    Mode = System.Windows.Data.BindingMode.TwoWay
                });
                KeyEditorPanel.Children.Add(keyTextBox);
            }
        }

        private void CreateValueEditor()
        {
            // For value types that are classes (excluding string), use PropertyEditor
            if (IsEditableClass(_valueType))
            {
                CreateClassEditor(ValueEditorPanel, _wrapper.Value, _valueType, "Value");
                return;
            }
            
            // For nested lists
            if (IsGenericList(_valueType))
            {
                CreateNestedListEditor(ValueEditorPanel, _valueType, "Value");
                return;
            }

            var valueProperty = typeof(KeyValueWrapper).GetProperty(nameof(KeyValueWrapper.Value))!;
            var valueEditor = CreateEditorForType(_valueType, valueProperty, _wrapper);
            if (valueEditor != null)
            {
                ValueEditorPanel.Children.Add(valueEditor);
            }
            else
            {
                // Fallback: simple textbox
                var valueTextBox = new TextBox { Margin = new Thickness(5) };
                valueTextBox.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(KeyValueWrapper.Value))
                {
                    Source = _wrapper,
                    Mode = System.Windows.Data.BindingMode.TwoWay
                });
                ValueEditorPanel.Children.Add(valueTextBox);
            }
        }

        private static bool IsEditableClass(Type type)
        {
            // A class is editable if it's a class type, not a string, not a primitive collection
            return type.IsClass && 
                   type != typeof(string) && 
                   !IsGenericList(type) &&
                   !typeof(IDictionary).IsAssignableFrom(type);
        }

        private static bool IsGenericList(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
        }

        private void CreateClassEditor(StackPanel panel, object? currentValue, Type type, string propertyName)
        {
            // Ensure we have a valid instance
            if (currentValue == null)
            {
                try
                {
                    currentValue = Activator.CreateInstance(type);
                    if (propertyName == "Key")
                        _wrapper.Key = currentValue;
                    else
                        _wrapper.Value = currentValue;
                }
                catch
                {
                    // Cannot create instance, fall back to textbox
                    var textBox = new TextBox { Margin = new Thickness(5) };
                    textBox.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(propertyName)
                    {
                        Source = _wrapper,
                        Mode = System.Windows.Data.BindingMode.TwoWay
                    });
                    panel.Children.Add(textBox);
                    return;
                }
            }

            // Use PropertyEditorHelper to generate the editor for the class
            var classEditor = PropertyEditorHelper.GenPropertyEditorControl(currentValue);
            panel.Children.Add(classEditor);
        }

        private void CreateNestedListEditor(StackPanel panel, Type listType, string propertyName)
        {
            var innerElementType = listType.GetGenericArguments()[0];
            
            // Get or create list instance
            object? listValue = propertyName == "Key" ? _wrapper.Key : _wrapper.Value;
            if (listValue == null)
            {
                listValue = Activator.CreateInstance(listType);
                if (propertyName == "Key")
                    _wrapper.Key = listValue;
                else
                    _wrapper.Value = listValue;
            }

            var list = listValue as IList;

            // Display current list info
            var infoText = new TextBlock
            {
                Text = list != null ? $"当前包含 {list.Count} 个项" : "空列表",
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = PropertyEditorHelper.GlobalTextBrush
            };
            panel.Children.Add(infoText);

            // Edit button to open nested list editor
            var editButton = new Button
            {
                Content = "编辑列表...",
                Width = 120,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 0)
            };
            
            editButton.Click += (s, e) =>
            {
                if (list != null)
                {
                    var nestedEditor = new ListEditorWindow(list, innerElementType);
                    nestedEditor.Owner = this;
                    
                    if (nestedEditor.ShowDialog() == true)
                    {
                        // Update the info text
                        infoText.Text = $"当前包含 {list.Count} 个项";
                    }
                }
            };

            panel.Children.Add(editButton);
        }

        private static DockPanel? CreateEditorForType(Type type, PropertyInfo property, object obj)
        {
            // Try to get a registered editor for this type
            var editorType = PropertyEditorHelper.GetEditorTypeForPropertyType(type);
            if (editorType != null)
            {
                try
                {
                    var editor = PropertyEditorHelper.GetOrCreateEditor(editorType);
                    
                    // Create a custom PropertyInfo that reports the correct type
                    var customProperty = new CustomPropertyInfo(property.Name, type, obj.GetType());
                    return editor.GenProperties(customProperty, obj);
                }
                catch
                {
                    // Fall through to default handling
                }
            }

            // For enums, create a ComboBox
            if (type.IsEnum)
            {
                var dockPanel = new DockPanel();
                var label = new TextBlock
                {
                    Text = property.Name == "Key" ? "键:" : "值:",
                    MinWidth = 60,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                var comboBox = new ComboBox
                {
                    ItemsSource = Enum.GetValues(type),
                    Margin = new Thickness(5)
                };
                comboBox.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, 
                    new System.Windows.Data.Binding(property.Name)
                    {
                        Source = obj,
                        Mode = System.Windows.Data.BindingMode.TwoWay
                    });
                
                dockPanel.Children.Add(label);
                dockPanel.Children.Add(comboBox);
                return dockPanel;
            }

            // For primitive types, we'll let the fallback textbox handle it
            return null;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate key is not null and not duplicate
            if (_wrapper.Key == null)
            {
                MessageBox.Show("键不能为空！", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if key already exists (only for new items or changed keys)
            foreach (var existingKey in _existingKeys)
            {
                if (Equals(existingKey, _wrapper.Key))
                {
                    MessageBox.Show($"键 '{_wrapper.Key}' 已存在！请使用不同的键。", "验证错误", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // Helper class to create a PropertyInfo for dynamic types
        // Note: This is a minimal implementation used only for type-based editor selection.
        // Attributes like DisplayName, Description are not needed since editors are selected by type.
        private sealed class CustomPropertyInfo : PropertyInfo
        {
            private readonly string _name;
            private readonly Type _propertyType;
            private readonly Type _declaringType;

            public CustomPropertyInfo(string name, Type propertyType, Type declaringType)
            {
                _name = name;
                _propertyType = propertyType;
                _declaringType = declaringType;
            }

            public override string Name => _name;
            public override Type PropertyType => _propertyType;
            public override Type? DeclaringType => _declaringType;
            public override Type? ReflectedType => _declaringType;
            public override PropertyAttributes Attributes => PropertyAttributes.None;
            public override bool CanRead => true;
            public override bool CanWrite => true;

            public override object[] GetCustomAttributes(bool inherit) => Array.Empty<object>();
            public override object[] GetCustomAttributes(Type attributeType, bool inherit) => Array.Empty<object>();
            public override bool IsDefined(Type attributeType, bool inherit) => false;

            public override MethodInfo[] GetAccessors(bool nonPublic) => Array.Empty<MethodInfo>();
            public override MethodInfo? GetGetMethod(bool nonPublic) => null;
            public override MethodInfo? GetSetMethod(bool nonPublic) => null;
            public override ParameterInfo[] GetIndexParameters() => Array.Empty<ParameterInfo>();

            public override object? GetValue(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? index, System.Globalization.CultureInfo? culture)
            {
                if (obj == null) return null;
                var prop = obj.GetType().GetProperty(_name);
                return prop?.GetValue(obj);
            }

            public override void SetValue(object? obj, object? value, BindingFlags invokeAttr, Binder? binder, object?[]? index, System.Globalization.CultureInfo? culture)
            {
                if (obj == null) return;
                var prop = obj.GetType().GetProperty(_name);
                prop?.SetValue(obj, value);
            }
        }
    }
}
