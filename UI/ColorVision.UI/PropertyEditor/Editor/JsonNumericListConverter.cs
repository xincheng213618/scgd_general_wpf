using ColorVision.UI;
using ColorVision.UI.PropertyEditor.Editor.List;
using Newtonsoft.Json;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace System.ComponentModel
{    
    // 通用：为所有数值型的 List<T> 注册 JSON 文本编辑器
    public class ListNumericJsonEditor : IPropertyEditor
    {
        static ListNumericJsonEditor()
        {
            // 通过谓词一次性注册：匹配 List<T> 且 T 为数值类型、字符串或枚举
            PropertyEditorHelper.RegisterEditor<ListNumericJsonEditor>(t =>
            {
                t = Nullable.GetUnderlyingType(t) ?? t;
                if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(System.Collections.Generic.List<>))
                    return false;

                var elem = t.GetGenericArguments()[0];
                elem = Nullable.GetUnderlyingType(elem) ?? elem;
                return elem == typeof(byte) || elem == typeof(sbyte) ||
                       elem == typeof(short) || elem == typeof(ushort) ||
                       elem == typeof(int) || elem == typeof(uint) ||
                       elem == typeof(long) || elem == typeof(ulong) ||
                       elem == typeof(float) || elem == typeof(double) ||
                       elem == typeof(decimal) || elem == typeof(string) ||
                       elem.IsEnum;
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
                // JSON 在输入过程中容易“半成品”，建议失焦再提交，避免每击键即报错
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
                Converter = new JsonNumericListConverter(),
                ValidatesOnExceptions = true,
                NotifyOnValidationError = true,
            };

            var textBox = PropertyEditorHelper.CreateSmallTextBox(binding);
            textBox.ToolTip = "输入 JSON 数组，例如: [1, 2, 3]";
            textBox.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;

            // 添加编辑按钮
            var editButton = new Button
            {
                Content = "编辑",
                Margin = new Thickness(5, 0, 0, 0),
                Width = 60
            };
            editButton.Click += (s, e) =>
            {
                var list = property.GetValue(obj) as IList;
                if (list != null)
                {
                    var elementType = property.PropertyType.GetGenericArguments()[0];
                    var editorWindow = new ListEditorWindow(list, elementType);
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


    public class JsonNumericListConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Collections.IEnumerable enumerable)
            {
                // 将任意 List<T> 序列化为 JSON
                return JsonConvert.SerializeObject(value, Formatting.None);
            }
            // 空列表显示为 []
            return "[]";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value?.ToString();

            // 目标必须是 List<T>
            if (!IsGenericList(targetType))
                throw new NotSupportedException($"Only List<T> is supported. TargetType: {targetType}");

            var elemType = targetType.GetGenericArguments()[0];
            if (!IsNumericType(elemType))
                throw new NotSupportedException($"Only numeric element types are supported. ElementType: {elemType}");

            if (string.IsNullOrWhiteSpace(s))
            {
                // 空输入 -> 空列表
                return Activator.CreateInstance(targetType)!;
            }

            try
            {
                var concreteListType = typeof(List<>).MakeGenericType(elemType);
                var result = JsonConvert.DeserializeObject(s, concreteListType);
                // 确保返回确切的目标类型（避免出现 List<T> 以外的集合类型）
                if (result == null)
                    return Activator.CreateInstance(targetType)!;

                if (result.GetType() == targetType)
                    return result;

                // 如果是 List<T> 就直接返回；否则尝试拷贝到目标类型（一般不会进到这里）
                var targetList = (System.Collections.IList)Activator.CreateInstance(targetType)!;
                foreach (var item in (System.Collections.IEnumerable)result)
                    targetList.Add(item);
                return targetList;
            }
            catch (Exception)
            {
                // 触发 WPF 校验错误显示
                throw new FormatException($"JSON 格式不正确，示例: [1, 2, 3]");
            }
        }

        private static bool IsGenericList(Type t)
            => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>);

        private static bool IsNumericType(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            return t == typeof(byte) || t == typeof(sbyte) ||
                   t == typeof(short) || t == typeof(ushort) ||
                   t == typeof(int) || t == typeof(uint) ||
                   t == typeof(long) || t == typeof(ulong) ||
                   t == typeof(float) || t == typeof(double) ||
                   t == typeof(decimal) || t == typeof(string) ||
                   t.IsEnum;
        }
    }
}
