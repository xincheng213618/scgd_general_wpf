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
            PropertyEditorHelper.RegisterEditor<EnumPropertiesEditor>(t => t.IsEnum);
        }

        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();

            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            var comboBox = new ComboBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = PropertyEditorHelper.ControlMinWidth,
                Style = PropertyEditorHelper.ComboBoxSmallStyle,
                ItemsSource = Enum.GetValues(property.PropertyType)
            };

            var binding = PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name);
            comboBox.SetBinding(Selector.SelectedItemProperty, binding);
            DockPanel.SetDock(comboBox, Dock.Right);

            dockPanel.Children.Add(comboBox);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }
    }
}
