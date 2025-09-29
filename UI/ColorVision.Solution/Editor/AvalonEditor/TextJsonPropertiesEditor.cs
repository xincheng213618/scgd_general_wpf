using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Solution.Editor.AvalonEditor
{
    public class TextJsonPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            var editCmd = new RelayCommand(_ =>
            {
                var owner = Application.Current.GetActiveWindow();
                var wnd = new AvalonEditWindow { WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = owner };
                wnd.SetJsonText(property.GetValue(obj) as string ?? string.Empty);
                wnd.Closing += (_, __) => property.SetValue(obj, wnd.GetJsonText());
                wnd.ShowDialog();
            });

            var iconBtn = PropertyEditorHelper.CreateIconSpinButton(editCmd);
            var binding = PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name);
            var textbox = PropertyEditorHelper.CreateSmallTextBox(binding);

            DockPanel.SetDock(iconBtn, Dock.Right);
            dockPanel.Children.Add(iconBtn);
            dockPanel.Children.Add(textbox);
            return dockPanel;
        }
    }
}
