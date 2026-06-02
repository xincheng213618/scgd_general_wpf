using ColorVision.UI;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace System.ComponentModel
{
    public class BoolPropertiesEditor : IPropertyEditor
    {
        static BoolPropertiesEditor()
        {
            PropertyEditorHelper.RegisterEditor<BoolPropertiesEditor>(t => (Nullable.GetUnderlyingType(t) ?? t) == typeof(bool));
        }

        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();

            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            var toggleSwitch = new ColorVision.Themes.Controls.ToggleSwitch
            {
                Margin = new Thickness(5, 0, 0, 0),
                Background = Brushes.DodgerBlue,
                IsThreeState = Nullable.GetUnderlyingType(property.PropertyType) != null
            };
            var binding = PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name);
            toggleSwitch.SetBinding(ToggleButton.IsCheckedProperty, binding);
            DockPanel.SetDock(toggleSwitch, Dock.Right);

            dockPanel.Children.Add(toggleSwitch);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }
    }
}
