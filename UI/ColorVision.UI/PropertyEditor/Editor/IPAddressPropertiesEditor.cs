using ColorVision.Themes.Controls;
using ColorVision.UI;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace System.ComponentModel;

public class IPAddressPropertiesEditor : IPropertyEditor
{
    protected virtual bool AllowTextMode => false;
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        var panel = new DockPanel();
        panel.Children.Add(PropertyEditorHelper.CreateLabel(property, PropertyEditorHelper.GetResourceManager(obj)));
        var editor = new IPAddressBox
        {
            AllowTextMode = AllowTextMode,
            IsReadOnly = !property.CanWrite || property.GetCustomAttribute<ReadOnlyAttribute>()?.IsReadOnly == true,
            MinWidth = PropertyEditorHelper.ControlMinWidth,
            Margin = new Thickness(5, 0, 0, 0)
        };
        editor.SetBinding(IPAddressBox.TextProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property));
        panel.Children.Add(editor);
        return panel;
    }
}

public class NetworkAddressPropertiesEditor : IPAddressPropertiesEditor
{
    protected override bool AllowTextMode => true;
}
