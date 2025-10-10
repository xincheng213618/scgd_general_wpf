using ColorVision.UI;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace System.ComponentModel
{
    public class TextBaudRatePropertiesEditor : IPropertyEditor
    {
        private static readonly List<int> BaudRates = new() { 921600, 460800, 230400, 115200, 57600, 38400, 19200, 14400, 9600, 4800, 2400, 1200, 600, 300 };
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            var combo = new HandyControl.Controls.ComboBox { Margin = new Thickness(5, 0, 0, 0), Style = PropertyEditorHelper.ComboBoxSmallStyle, IsEditable = true, ItemsSource = BaudRates };
            HandyControl.Controls.InfoElement.SetShowClearButton(combo, true);
            combo.SetBinding(ComboBox.TextProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name));
            dockPanel.Children.Add(combo);
            return dockPanel;
        }
    }
}
