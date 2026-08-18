using ColorVision.UI;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace System.ComponentModel
{
    public class CommandPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();

            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            var command = property.GetValue(obj) as ICommand;

            var button = new Button
            {
                Margin = new Thickness(5, 0, 0, 0),
                Content = ColorVision.UI.Properties.Resources.Execute,
                Command = command
            };

            DockPanel.SetDock(button, Dock.Right);
            dockPanel.Children.Add(button);
            dockPanel.Children.Add(textBlock);


            return dockPanel;
        }
    }
}
