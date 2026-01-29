using ColorVision.UI;
using ColorVision.UI.PropertyEditor.Editor.Dictionary;
using Newtonsoft.Json;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace System.ComponentModel
{
    // 为 Dictionary<TKey, TValue> 注册 JSON 文本编辑器
    public class DictionaryJsonEditor : IPropertyEditor
    {
        static DictionaryJsonEditor()
        {
            // 通过谓词注册：匹配 Dictionary<TKey, TValue> 和 IDictionary<TKey, TValue>
            PropertyEditorHelper.RegisterEditor<DictionaryJsonEditor>(t =>
            {
                t = Nullable.GetUnderlyingType(t) ?? t;
                if (!t.IsGenericType)
                    return false;
                
                var genericTypeDef = t.GetGenericTypeDefinition();
                
                // 支持 Dictionary<TKey, TValue> 和 IDictionary<TKey, TValue>
                return genericTypeDef == typeof(System.Collections.Generic.Dictionary<,>) ||
                       genericTypeDef == typeof(System.Collections.Generic.IDictionary<,>);
            });
        }

        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();

            var label = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(label);

            var binding = new Binding(property.Name)
            {
                Source = obj,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
                Converter = new JsonDictionaryConverter(),
                ValidatesOnExceptions = true,
                NotifyOnValidationError = true,
            };

            var textBox = PropertyEditorHelper.CreateSmallTextBox(binding);
            textBox.ToolTip = "输入 JSON 对象，例如: {\"key1\": \"value1\", \"key2\": \"value2\"}";
            textBox.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;

            // 添加编辑按钮
            var editButton = new Button
            {
                Content = ColorVision.UI.Properties.Resources.Edit,
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = 60
            };
            editButton.Click += (s, e) =>
            {
                var dictionary = property.GetValue(obj) as IDictionary;
                if (dictionary != null)
                {
                    var genericArgs = property.PropertyType.GetGenericArguments();
                    var keyType = genericArgs[0];
                    var valueType = genericArgs[1];
                    
                    var editorWindow = new DictionaryEditorWindow(dictionary, keyType, valueType);
                    editorWindow.Owner = Window.GetWindow(dockPanel);
                    
                    if (editorWindow.ShowDialog() == true)
                    {
                        // 更新 TextBox 显示
                        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                    }
                }
            };

            DockPanel.SetDock(editButton, Dock.Right);
            dockPanel.Children.Add(editButton);
            dockPanel.Children.Add(textBox);
            return dockPanel;
        }
    }

    public class JsonDictionaryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IDictionary dictionary)
            {
                // 将 Dictionary 序列化为 JSON
                return JsonConvert.SerializeObject(value, Formatting.None);
            }
            // 空字典显示为 {}
            return "{}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value?.ToString();

            // 目标必须是支持的字典类型
            if (!IsSupportedDictionaryType(targetType))
                throw new NotSupportedException($"Dictionary type {targetType} is not supported. Supported types: Dictionary<TKey, TValue>, IDictionary<TKey, TValue>");

            var genericArgs = targetType.GetGenericArguments();
            if (genericArgs.Length != 2)
                throw new NotSupportedException($"Invalid dictionary type: {targetType}");

            var keyType = genericArgs[0];
            var valueType = genericArgs[1];

            if (string.IsNullOrWhiteSpace(s))
            {
                // 空输入 -> 空字典
                return CreateEmptyDictionary(targetType, keyType, valueType);
            }

            try
            {
                // 先反序列化为 Dictionary<TKey, TValue>
                var concreteDictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
                var result = JsonConvert.DeserializeObject(s, concreteDictType);
                
                if (result == null)
                    return CreateEmptyDictionary(targetType, keyType, valueType);

                // 如果目标类型就是 Dictionary，直接返回
                if (result.GetType() == targetType)
                    return result;

                // 如果目标是 IDictionary<TKey, TValue>，返回 Dictionary
                var genericTypeDef = targetType.GetGenericTypeDefinition();
                if (genericTypeDef == typeof(System.Collections.Generic.IDictionary<,>))
                {
                    return result;
                }

                return result;
            }
            catch (Exception)
            {
                // 触发 WPF 校验错误显示
                throw new FormatException($"JSON 格式不正确，示例: {{\"key1\": \"value1\", \"key2\": \"value2\"}}");
            }
        }

        private static bool IsSupportedDictionaryType(Type t)
        {
            if (!t.IsGenericType)
                return false;
                
            var genericTypeDef = t.GetGenericTypeDefinition();
            return genericTypeDef == typeof(Dictionary<,>) ||
                   genericTypeDef == typeof(System.Collections.Generic.IDictionary<,>);
        }

        private static object CreateEmptyDictionary(Type targetType, Type keyType, Type valueType)
        {
            var genericTypeDef = targetType.GetGenericTypeDefinition();
            
            if (genericTypeDef == typeof(System.Collections.Generic.IDictionary<,>))
            {
                // For interface, return a Dictionary
                return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(keyType, valueType))!;
            }
            
            // Default to Dictionary<TKey, TValue>
            return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(keyType, valueType))!;
        }
    }
}
