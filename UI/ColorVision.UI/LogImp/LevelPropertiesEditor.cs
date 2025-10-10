using ColorVision.UI;
using log4net.Core;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.LogImp
{
    public class LevelPropertiesEditor : IPropertyEditor
    {
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
                ItemsSource = TypeLevelCacheHelper.GetAllLevels<Level>(typeof(Level))
                    .Select(p => new KeyValuePair<Level, string>(p, p.Name)).ToList()
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
