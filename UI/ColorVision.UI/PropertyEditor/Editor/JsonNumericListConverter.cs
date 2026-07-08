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
    public class CollectionJsonEditor : IPropertyEditor
    {
        static CollectionJsonEditor()
        {
            PropertyEditorHelper.RegisterEditor<CollectionJsonEditor>(t =>
            {
                t = Nullable.GetUnderlyingType(t) ?? t;
                return CollectionTypeHelper.IsSupportedCollectionType(t) &&
                       CollectionTypeHelper.TryGetElementType(t, out var elementType) &&
                       CollectionTypeHelper.IsSupportedElementType(elementType);
            });
        }

        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            dockPanel.Children.Add(PropertyEditorHelper.CreateLabel(property, rm));

            var binding = new Binding(property.Name)
            {
                Source = obj,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
                Converter = new CollectionJsonValueConverter(),
                ValidatesOnExceptions = true,
                NotifyOnValidationError = true
            };

            var textBox = PropertyEditorHelper.CreateSmallTextBox(binding);
            textBox.ToolTip = "输入 JSON 数组，例如: [1, 2, 3]";
            textBox.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;

            if (CollectionTypeHelper.TryGetElementType(property.PropertyType, out var elementType) &&
                CollectionTypeHelper.CanUseListDialog(elementType))
            {
                var editButton = CreateEditButton(property, obj, dockPanel, textBox, elementType);
                DockPanel.SetDock(editButton, Dock.Right);
                dockPanel.Children.Add(editButton);
            }

            dockPanel.Children.Add(textBox);
            return dockPanel;
        }

        private static Button CreateEditButton(PropertyInfo property, object obj, DockPanel dockPanel, TextBox textBox, Type elementType)
        {
            var editButton = new Button
            {
                Content = ColorVision.UI.Properties.Resources.Edit,
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = 60
            };

            editButton.Click += (_, _) =>
            {
                var collection = property.GetValue(obj);
                if (collection == null)
                    return;

                var list = CollectionTypeHelper.CreateEditableList(collection, elementType);
                if (list == null)
                    return;

                var collectionEditorAttr = property.GetCustomAttribute<CollectionEditorTypeAttribute>();
                var editorWindow = new ListEditorWindow(list, elementType, collectionEditorAttr?.ItemEditorType)
                {
                    Owner = Window.GetWindow(dockPanel)
                };

                if (editorWindow.ShowDialog() == true)
                {
                    property.SetValue(obj, CollectionTypeHelper.ConvertListToDeclaredType(property.PropertyType, list, elementType));
                    textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                }
            };

            return editButton;
        }
    }

    public class CollectionJsonValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is IEnumerable ? JsonConvert.SerializeObject(value, Formatting.None) : "[]";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value?.ToString();
            if (!CollectionTypeHelper.IsSupportedCollectionType(targetType))
                throw new NotSupportedException($"Collection type {targetType} is not supported. Supported types: arrays, List<T>, ObservableCollection<T>, Collection<T>, IList<T>, ICollection<T>, IEnumerable<T>.");

            if (!CollectionTypeHelper.TryGetElementType(targetType, out var elementType))
                throw new NotSupportedException($"Collection type {targetType} is missing an element type.");

            if (!CollectionTypeHelper.IsSupportedElementType(elementType))
                throw new NotSupportedException($"Element type {elementType} is not supported. Supported element types: numeric types, string, enum, nested collections, simple structs, or editable configuration classes.");

            if (string.IsNullOrWhiteSpace(text))
                return CollectionTypeHelper.CreateEmptyCollection(targetType, elementType);

            try
            {
                var listType = typeof(List<>).MakeGenericType(elementType);
                var result = JsonConvert.DeserializeObject(text, listType);
                if (result is not IList list)
                    return CollectionTypeHelper.CreateEmptyCollection(targetType, elementType);

                return CollectionTypeHelper.ConvertListToDeclaredType(targetType, list, elementType);
            }
            catch
            {
                throw new FormatException("JSON 格式不正确，示例: [1, 2, 3]");
            }
        }
    }

    public class ListNumericJsonEditor : CollectionJsonEditor
    {
    }

    public class JsonNumericListConverter : CollectionJsonValueConverter
    {
    }
}
