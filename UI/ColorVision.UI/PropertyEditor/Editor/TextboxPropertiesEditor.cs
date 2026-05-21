using ColorVision.UI;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Data;

namespace System.ComponentModel
{
    public class TextboxPropertiesEditor : IPropertyEditor
    {
        private static readonly HashSet<Type> TextEditableTypes = new()
        {
            typeof(int),
            typeof(short),
            typeof(ushort),
            typeof(float),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(sbyte),
            typeof(double),
            typeof(decimal),
            typeof(byte),
            typeof(char),
            typeof(Guid),
            typeof(string)
        };

        static TextboxPropertiesEditor()
        {
            PropertyEditorHelper.RegisterEditor<TextboxPropertiesEditor>(t =>
            {
                t = Nullable.GetUnderlyingType(t) ?? t;
                return TextEditableTypes.Contains(t);
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
            if (t == typeof(float) || t == typeof(double))
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
