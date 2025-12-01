using ColorVision.UI;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace System.ComponentModel
{
    public class TextboxPropertiesEditor : IPropertyEditor
    {
        static TextboxPropertiesEditor()
        {
            PropertyEditorHelper.RegisterEditor<TextboxPropertiesEditor>(t =>
            {
                t = Nullable.GetUnderlyingType(t) ?? t;
                return t == typeof(int) ||
                       t == typeof(float) ||
                       t == typeof(uint) ||
                       t == typeof(long) ||
                       t == typeof(ulong) ||
                       t == typeof(sbyte) ||
                       t == typeof(double) ||
                       t == typeof(decimal) ||
                       t == typeof(byte) ||
                       t == typeof(string)||
                       t == typeof(System.Windows.Rect)  ;
            });
        }
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
