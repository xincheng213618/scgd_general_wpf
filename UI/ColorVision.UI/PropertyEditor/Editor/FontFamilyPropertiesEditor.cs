using ColorVision.UI;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace System.ComponentModel
{
    public class FontFamilyPropertiesEditor : IPropertyEditor
    {
        static FontFamilyPropertiesEditor()
        {
            PropertyEditorHelper.RegisterEditor<FontFamilyPropertiesEditor>(typeof(FontFamily));
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
                ItemsSource = Fonts.SystemFontFamilies
                    .Select(f => new KeyValuePair<FontFamily, string>(
                        f,
                        f.FamilyNames.TryGetValue(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.Name), out string fontName) ? fontName : f.Source
                    )).ToList()
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
