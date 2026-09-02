using ColorVision.UI;
using ColorVision.Common.Utilities;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace System.ComponentModel
{
    public class EnumPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();

            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            var enumType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var values = Enum.GetValues(enumType)
                .Cast<Enum>()
                .Select(value => new KeyValuePair<object?, string>(
                    value,
                    GetDisplayText(rm, value)))
                .ToList();
            if (Nullable.GetUnderlyingType(property.PropertyType) != null)
            {
                values.Insert(0, new KeyValuePair<object?, string>(null, string.Empty));
            }

            var comboBox = new ComboBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = PropertyEditorHelper.ControlMinWidth,
                Style = PropertyEditorHelper.ComboBoxSmallStyle,
                ItemsSource = values,
                DisplayMemberPath = nameof(KeyValuePair<object?, string>.Value),
                SelectedValuePath = nameof(KeyValuePair<object?, string>.Key)
            };

            var binding = PropertyEditorHelper.CreateTwoWayBinding(obj, property);
            comboBox.SetBinding(Selector.SelectedValueProperty, binding);

            dockPanel.Children.Add(textBlock);
            dockPanel.Children.Add(comboBox);
            return dockPanel;
        }

        private static string GetDisplayText(ResourceManager? resourceManager, Enum value)
        {
            try
            {
                // Existing enum-name translations take priority, including translations identical to the key.
                string? localizedName = resourceManager?.GetString(value.ToString(), CultureInfo.CurrentUICulture);
                if (localizedName != null) return localizedName;
            }
            catch
            {
                // Match the property editor's existing resource lookup fallback.
            }

            return PropertyEditorHelper.GetLocalizedString(resourceManager, value.ToDescription());
        }
    }
}
