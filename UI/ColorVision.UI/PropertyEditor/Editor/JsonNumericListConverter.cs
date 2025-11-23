using ColorVision.UI;
using ColorVision.UI.PropertyEditor.Editor.List;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace System.ComponentModel
{    
    // 通用：为所有支持的集合类型注册 JSON 文本编辑器（List<T>、ObservableCollection<T>、Collection<T> 等）
    public class ListNumericJsonEditor : IPropertyEditor
    {
        static ListNumericJsonEditor()
        {
            // 通过谓词一次性注册：匹配支持的集合类型
            PropertyEditorHelper.RegisterEditor<ListNumericJsonEditor>(t =>
            {
                t = Nullable.GetUnderlyingType(t) ?? t;
                if (!t.IsGenericType)
                    return false;
                
                var genericTypeDef = t.GetGenericTypeDefinition();
                
                // 支持 List<T>, ObservableCollection<T>, Collection<T>, IList<T>, ICollection<T>, IEnumerable<T>
                return genericTypeDef == typeof(System.Collections.Generic.List<>) ||
                       genericTypeDef == typeof(ObservableCollection<>) ||
                       genericTypeDef == typeof(Collection<>) ||
                       genericTypeDef == typeof(System.Collections.Generic.IList<>) ||
                       genericTypeDef == typeof(System.Collections.Generic.ICollection<>) ||
                       genericTypeDef == typeof(System.Collections.Generic.IEnumerable<>);
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
                // 获取集合实例
                var collection = property.GetValue(obj);
                if (collection != null)
                {
                    // 确保集合实现了 IList 接口（用于可修改的集合）
                    IList? list = null;
                    
                    if (collection is IList ilist)
                    {
                        list = ilist;
                    }
                    else if (collection is System.Collections.IEnumerable enumerable)
                    {
                        // 对于只读集合（如 IEnumerable），创建临时 List 用于编辑
                        var elementType = property.PropertyType.GetGenericArguments()[0];
                        var listType = typeof(List<>).MakeGenericType(elementType);
                        list = (IList)Activator.CreateInstance(listType)!;
                        foreach (var item in enumerable)
                        {
                            list.Add(item);
                        }
                    }
                    
                    if (list != null)
                    {
                        var elementType = property.PropertyType.GetGenericArguments()[0];
                        var editorWindow = new ListEditorWindow(list, elementType);
                        editorWindow.Owner = Window.GetWindow(dockPanel);
                        
                        if (editorWindow.ShowDialog() == true)
                        {
                            // 如果原集合不是 IList，需要重新创建集合并赋值
                            if (collection is not IList)
                            {
                                var newCollection = CreateCollectionFromList(property.PropertyType, list, elementType);
                                property.SetValue(obj, newCollection);
                            }
                            
                            // 更新 TextBox 显示
                            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                        }
                    }
                }
            };

            DockPanel.SetDock(editButton, Dock.Right);
            dockPanel.Children.Add(editButton);
            dockPanel.Children.Add(textBox);
            return dockPanel;
        }
        
        private static object CreateCollectionFromList(Type targetType, IList list, Type elementType)
        {
            var genericTypeDef = targetType.GetGenericTypeDefinition();
            
            if (genericTypeDef == typeof(ObservableCollection<>))
            {
                var obsCollType = typeof(ObservableCollection<>).MakeGenericType(elementType);
                var obsColl = (IList)Activator.CreateInstance(obsCollType)!;
                foreach (var item in list)
                    obsColl.Add(item);
                return obsColl;
            }
            else if (genericTypeDef == typeof(Collection<>))
            {
                var collType = typeof(Collection<>).MakeGenericType(elementType);
                var coll = (IList)Activator.CreateInstance(collType)!;
                foreach (var item in list)
                    coll.Add(item);
                return coll;
            }
            else if (genericTypeDef == typeof(System.Collections.Generic.IList<>) ||
                     genericTypeDef == typeof(System.Collections.Generic.ICollection<>) ||
                     genericTypeDef == typeof(System.Collections.Generic.IEnumerable<>))
            {
                // For interfaces, return a List<T>
                return list;
            }
            
            return list;
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

            // 目标必须是支持的集合类型
            if (!IsSupportedCollectionType(targetType))
                throw new NotSupportedException($"Collection type {targetType} is not supported. Supported types: List<T>, ObservableCollection<T>, Collection<T>, IList<T>, ICollection<T>, IEnumerable<T>. Element types must be numeric, string, enum, or have a registered property editor.");

            var elemType = targetType.GetGenericArguments()[0];
            if (!IsNumericType(elemType))
                throw new NotSupportedException($"Element type {elemType} is not supported. Supported element types: numeric types (int, double, etc.), string, enum.");

            if (string.IsNullOrWhiteSpace(s))
            {
                // 空输入 -> 空集合
                return CreateEmptyCollection(targetType, elemType);
            }

            try
            {
                // 先反序列化为 List<T>
                var concreteListType = typeof(List<>).MakeGenericType(elemType);
                var result = JsonConvert.DeserializeObject(s, concreteListType);
                
                if (result == null)
                    return CreateEmptyCollection(targetType, elemType);

                // 转换为目标集合类型
                return ConvertToTargetCollection(result as IList, targetType, elemType);
            }
            catch (Exception)
            {
                // 触发 WPF 校验错误显示
                throw new FormatException($"JSON 格式不正确，示例: [1, 2, 3]");
            }
        }

        private static bool IsSupportedCollectionType(Type t)
        {
            if (!t.IsGenericType)
                return false;
                
            var genericTypeDef = t.GetGenericTypeDefinition();
            return genericTypeDef == typeof(List<>) ||
                   genericTypeDef == typeof(ObservableCollection<>) ||
                   genericTypeDef == typeof(Collection<>) ||
                   genericTypeDef == typeof(System.Collections.Generic.IList<>) ||
                   genericTypeDef == typeof(System.Collections.Generic.ICollection<>) ||
                   genericTypeDef == typeof(System.Collections.Generic.IEnumerable<>);
        }

        private static object CreateEmptyCollection(Type targetType, Type elementType)
        {
            var genericTypeDef = targetType.GetGenericTypeDefinition();
            
            if (genericTypeDef == typeof(ObservableCollection<>))
            {
                return Activator.CreateInstance(typeof(ObservableCollection<>).MakeGenericType(elementType))!;
            }
            else if (genericTypeDef == typeof(Collection<>))
            {
                return Activator.CreateInstance(typeof(Collection<>).MakeGenericType(elementType))!;
            }
            else if (genericTypeDef == typeof(System.Collections.Generic.IList<>) ||
                     genericTypeDef == typeof(System.Collections.Generic.ICollection<>) ||
                     genericTypeDef == typeof(System.Collections.Generic.IEnumerable<>))
            {
                // For interfaces, return a List<T>
                return Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
            }
            
            // Default to List<T>
            return Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        }

        private static object ConvertToTargetCollection(IList? sourceList, Type targetType, Type elementType)
        {
            if (sourceList == null)
                return CreateEmptyCollection(targetType, elementType);
                
            var genericTypeDef = targetType.GetGenericTypeDefinition();
            
            // If target is already the source type, return as-is
            if (sourceList.GetType() == targetType)
                return sourceList;
            
            if (genericTypeDef == typeof(ObservableCollection<>))
            {
                var obsCollType = typeof(ObservableCollection<>).MakeGenericType(elementType);
                var obsColl = (IList)Activator.CreateInstance(obsCollType)!;
                foreach (var item in sourceList)
                    obsColl.Add(item);
                return obsColl;
            }
            else if (genericTypeDef == typeof(Collection<>))
            {
                var collType = typeof(Collection<>).MakeGenericType(elementType);
                var coll = (IList)Activator.CreateInstance(collType)!;
                foreach (var item in sourceList)
                    coll.Add(item);
                return coll;
            }
            else if (genericTypeDef == typeof(System.Collections.Generic.IList<>) ||
                     genericTypeDef == typeof(System.Collections.Generic.ICollection<>) ||
                     genericTypeDef == typeof(System.Collections.Generic.IEnumerable<>))
            {
                // For interfaces, return the List<T>
                return sourceList;
            }
            
            // Default: return the List<T> 
            return sourceList;
        }

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
