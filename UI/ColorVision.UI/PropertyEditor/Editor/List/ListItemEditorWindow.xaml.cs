#pragma warning disable CA1310,CA1822
using ColorVision.UI.Properties;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.PropertyEditor.Editor.List
{
    public partial class ListItemEditorWindow : Window
    {
        private readonly Type _elementType;
        private readonly Type? _preferredEditorType;
        private readonly ValueWrapper _valueWrapper;
        private List<Type> _availableEditorTypes = new();

        public object? EditedValue => _valueWrapper.Value;

        // Wrapper class to hold the value as a property so we can use PropertyEditor system
        private sealed class ValueWrapper : INotifyPropertyChanged
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

        public ListItemEditorWindow(Type elementType, object? initialValue, Type? preferredEditorType = null)
        {
            InitializeComponent();
            _elementType = elementType;
            _preferredEditorType = preferredEditorType;
            _valueWrapper = new ValueWrapper { Value = initialValue ?? CreateEditableClassInstance(elementType) };

            InitializeEditorSelection();
            CreateEditor();
        }

        private void InitializeEditorSelection()
        {
            // Get all available editors for this type
            _availableEditorTypes = GetAvailableEditorTypes(_elementType);

            // If there are multiple editors, show the selection ComboBox
            if (_availableEditorTypes.Count > 1)
            {
                EditorTypePanel.Visibility = Visibility.Visible;
                
                // Populate ComboBox with editor names
                foreach (var editorType in _availableEditorTypes)
                {
                    var displayName = GetEditorDisplayName(editorType);
                    EditorTypeComboBox.Items.Add(new ComboBoxItem 
                    { 
                        Content = displayName,
                        Tag = editorType
                    });
                }
                
                // Select the first one by default
                EditorTypeComboBox.SelectedIndex = 0;
            }
        }

        private List<Type> GetAvailableEditorTypes(Type elementType)
        {
            if (IsValidEditorType(_preferredEditorType))
            {
                return new List<Type> { _preferredEditorType! };
            }

            var editorTypes = new List<Type>();

            // For strings, manually add specific editors
            if (elementType == typeof(string))
            {
                editorTypes.Add(typeof(TextboxPropertiesEditor));
                editorTypes.Add(typeof(TextSelectFilePropertiesEditor));
                editorTypes.Add(typeof(TextSelectFolderPropertiesEditor));
            }
            else
            {
                // Get all registered editors for this type
                editorTypes = PropertyEditorHelper.GetAllEditorTypesForPropertyType(elementType);
            }

            return editorTypes;
        }

        private static bool IsValidEditorType(Type? editorType)
        {
            return editorType != null && typeof(IPropertyEditor).IsAssignableFrom(editorType);
        }

        private string GetEditorDisplayName(Type editorType)
        {
            // Map editor types to user-friendly names
            var nameMap = new Dictionary<Type, string>
            {
                { typeof(TextSelectFilePropertiesEditor), Properties.Resources.ResourceManager.GetString("ListItemEditor_FileSelector", System.Globalization.CultureInfo.CurrentUICulture) ?? "File Selector" },
                { typeof(TextSelectFolderPropertiesEditor), Properties.Resources.ResourceManager.GetString("ListItemEditor_FolderSelector", System.Globalization.CultureInfo.CurrentUICulture) ?? "Folder Selector" },
                { typeof(TextboxPropertiesEditor), Properties.Resources.ResourceManager.GetString("ListItemEditor_Textbox", System.Globalization.CultureInfo.CurrentUICulture) ?? "Textbox" },
                { typeof(EnumPropertiesEditor), Properties.Resources.ResourceManager.GetString("ListItemEditor_Dropdown", System.Globalization.CultureInfo.CurrentUICulture) ?? "Dropdown" },
                { typeof(BoolPropertiesEditor), Properties.Resources.ResourceManager.GetString("ListItemEditor_Checkbox", System.Globalization.CultureInfo.CurrentUICulture) ?? "Checkbox" }
            };

            if (nameMap.TryGetValue(editorType, out var displayName))
                return displayName;

            // Fallback: use the class name without "PropertiesEditor"
            var name = editorType.Name;
            if (name.EndsWith("PropertiesEditor"))
                name = name.Substring(0, name.Length - "PropertiesEditor".Length);
            return name;
        }

        private void EditorTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EditorTypeComboBox.SelectedItem is ComboBoxItem item && item.Tag is Type)
            {
                // Recreate the editor with the selected type
                CreateEditor();
            }
        }

        private void CreateEditor()
        {
            // Clear existing editor
            EditorPanel.Children.Clear();

            // Special handling for nested lists (e.g., List<List<int>>)
            if (IsGenericList(_elementType))
            {
                CreateNestedListEditor();
                return;
            }
            // Special handling for class types (exclude string and other primitive-like types)
            else if (IsEditableClass(_elementType))
            {
                _valueWrapper.Value ??= CreateEditableClassInstance(_elementType);
                if (_valueWrapper.Value != null)
                {
                    EditorPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(_valueWrapper.Value));
                }
                else
                {
                    CreateFallbackEditor();
                }
                return;
            }

            // Get the base property from ValueWrapper
            var baseProperty = typeof(ValueWrapper).GetProperty(nameof(ValueWrapper.Value))!;
            
            // Determine which editor to use
            Type? editorType = null;
            
            if (_availableEditorTypes.Count > 1 && EditorTypeComboBox.SelectedItem is ComboBoxItem item && item.Tag is Type selectedType)
            {
                editorType = selectedType;
            }
            else if (_availableEditorTypes.Count == 1)
            {
                editorType = _availableEditorTypes[0];
            }
            else
            {
                editorType = DetermineEditorType(_elementType);
            }
            
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

        private void CreateNestedListEditor()
        {
            // Create UI for editing nested lists
            var label = new TextBlock
            {
                Text = Properties.Resources.ListItemEditor_NestedList,
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.Bold
            };
            EditorPanel.Children.Add(label);

            // Get the inner element type (e.g., for List<List<int>>, this would be List<int>)
            var innerElementType = _elementType.GetGenericArguments()[0];

            // Ensure we have a valid list instance
            if (_valueWrapper.Value == null)
            {
                var listType = typeof(List<>).MakeGenericType(innerElementType);
                _valueWrapper.Value = Activator.CreateInstance(listType);
            }

            var list = _valueWrapper.Value as IList;

            // Display current list info
            var infoText = new TextBlock
            {
                Text = list != null ? string.Format(Properties.Resources.DictItemEditor_Count, list.Count) : Properties.Resources.DictItemEditor_Empty,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = PropertyEditorHelper.GlobalTextBrush
            };
            EditorPanel.Children.Add(infoText);

            // Edit button to open nested list editor
            var editButton = new Button
            {
                Content = Properties.Resources.DictItemEditor_EditList,
                Width = 120,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 0)
            };
            
            editButton.Click += (s, e) =>
            {
                if (list != null)
                {
                    var nestedEditor = new ListEditorWindow(list, innerElementType, _preferredEditorType);
                    nestedEditor.Owner = this;
                    
                    if (nestedEditor.ShowDialog() == true)
                    {
                        // Update the info text
                        infoText.Text = string.Format(Properties.Resources.DictItemEditor_Count, list.Count);
                    }
                }
            };

            EditorPanel.Children.Add(editButton);
        }

        private static bool IsGenericList(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
        }

        private static bool IsEditableClass(Type type)
        {
            // A class is editable if it's a class type, not a string, not a primitive collection
            return type.IsClass && 
                   type != typeof(string) && 
                   !IsGenericList(type) &&
                   !typeof(System.Collections.IDictionary).IsAssignableFrom(type);
        }

        private static object? CreateEditableClassInstance(Type type)
        {
            if (!IsEditableClass(type) || type.IsAbstract || type.IsInterface)
            {
                return null;
            }

            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                return null;
            }

            try
            {
                return Activator.CreateInstance(type);
            }
            catch
            {
                return null;
            }
        }

        private Type? DetermineEditorType(Type elementType)
        {
            if (elementType == typeof(string))
            {
                return typeof(TextboxPropertiesEditor);
            }
            
            // Otherwise use the registered editor for the type
            return PropertyEditorHelper.GetEditorTypeForPropertyType(elementType);
        }

        private void CreateFallbackEditor()
        {
            var label = new TextBlock
            {
                Text = Properties.Resources.DictItemEditor_Value,
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
        private sealed class CustomPropertyInfo : PropertyInfo
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
