using ColorVision.UI;
using log4net;
using log4net.Core;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace System.ComponentModel
{

    public class FontStylePropertiesEditor : IPropertyEditor
    {
        static FontStylePropertiesEditor()
        {
            PropertyEditorHelper.RegisterEditor<FontFamilyPropertiesEditor>(typeof(FontStyle));
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
                DisplayMemberPath = "Value",
                SelectedValuePath = "Key",
                ItemsSource = typeof(FontStyles).GetProperties()
                    .Select(p => new KeyValuePair<FontStyle, string>((FontStyle)p.GetValue(null), p.Name)).ToList()
            };

            var binding = PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name);
            comboBox.SetBinding(ComboBox.SelectedValueProperty, binding);
            DockPanel.SetDock(comboBox, Dock.Right);

            dockPanel.Children.Add(comboBox);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }
    }
}
