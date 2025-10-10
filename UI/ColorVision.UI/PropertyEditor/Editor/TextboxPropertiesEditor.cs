using ColorVision.UI;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Data;

namespace System.ComponentModel
{
    public class TextboxPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            Binding binding = PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name);
            binding.UpdateSourceTrigger = UpdateSourceTrigger.Default;

            var t = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (t == typeof(float) || property.PropertyType == typeof(double))
            {
                binding.StringFormat = "0.0################";
            }

            var textbox = PropertyEditorHelper.CreateSmallTextBox(binding);
            textbox.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;
            dockPanel.Children.Add(textbox);
            return dockPanel;
        }
    }
}
