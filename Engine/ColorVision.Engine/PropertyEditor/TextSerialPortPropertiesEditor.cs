using ColorVision.UI;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.PropertyEditor
{
    public class TextSerialPortPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            string[] SerialPorts = SerialPort.GetPortNames();
            var combo = new HandyControl.Controls.ComboBox { Margin = new Thickness(5, 0, 0, 0), Style = PropertyEditorHelper.ComboBoxSmallStyle, IsEditable = true, ItemsSource = SerialPorts };
            HandyControl.Controls.InfoElement.SetShowClearButton(combo, true);
            combo.SetBinding(ComboBox.TextProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name));
            dockPanel.Children.Add(combo);
            return dockPanel;
        }
    }
}
