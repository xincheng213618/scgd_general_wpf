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
            var enumResources = PropertyEditorHelper.GetResourceManager(enumType);
            var values = Enum.GetValues(enumType)
                .Cast<Enum>()
                .Select(value => new KeyValuePair<object?, string>(
                    value,
                    GetDisplayText(rm, enumResources, value)))
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

        private static string GetDisplayText(ResourceManager? resourceManager, ResourceManager? enumResources, Enum value)
        {
            // The edited object and its enum can belong to different assemblies.
            // Keep host overrides first, then use the enum owner's translations.
            string name = value.ToString();
            string? localized = TryGetString(resourceManager, name) ?? TryGetString(enumResources, name);
            if (localized != null) return localized;

            string description = value.ToDescription();
            return TryGetString(resourceManager, description) ?? TryGetString(enumResources, description) ?? description;
        }

        private static string? TryGetString(ResourceManager? resourceManager, string key)
        {
            try
            {
                return resourceManager?.GetString(key, CultureInfo.CurrentUICulture);
            }
            catch
            {
                // Match the property editor's existing resource lookup fallback.
            }

            return null;
        }
    }
}
