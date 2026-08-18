using ColorVision.UI;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace System.ComponentModel
{
    public class EnumPropertiesEditor : IPropertyEditor
    {
        static EnumPropertiesEditor()
        {
            PropertyEditorHelper.RegisterEditor<EnumPropertiesEditor>(t => (Nullable.GetUnderlyingType(t) ?? t).IsEnum);
        }

        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();

            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            var enumType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var values = Enum.GetValues(enumType)
                .Cast<object>()
                .Select(value => new KeyValuePair<object?, string>(
                    value,
                    PropertyEditorHelper.GetLocalizedString(rm, value.ToString())))
                .ToList();
            if (Nullable.GetUnderlyingType(property.PropertyType) != null)
            {
                values.Insert(0, new KeyValuePair<object?, string>(null, string.Empty));
            }

            var comboBox = new ComboBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = PropertyEditorHelper.ControlMinWidth,
                Style = PropertyEditorHelper.ComboBoxSmallStyle,
                ItemsSource = values,
                DisplayMemberPath = nameof(KeyValuePair<object?, string>.Value),
                SelectedValuePath = nameof(KeyValuePair<object?, string>.Key)
            };

            var binding = PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name);
            comboBox.SetBinding(Selector.SelectedValueProperty, binding);

            dockPanel.Children.Add(textBlock);
            dockPanel.Children.Add(comboBox);
            return dockPanel;
        }
    }
}
