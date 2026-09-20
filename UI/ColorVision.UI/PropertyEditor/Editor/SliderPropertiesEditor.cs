using ColorVision.UI;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace System.ComponentModel
{
    /// <summary>Edits a ranged numeric property with both a slider and an exact-value text box.</summary>
    public sealed class SliderPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            RangeAttribute range = property.GetCustomAttribute<RangeAttribute>()
                ?? throw new InvalidOperationException($"{nameof(SliderPropertiesEditor)} requires {nameof(RangeAttribute)} on {property.Name}.");
            double minimum = Convert.ToDouble(range.Minimum, CultureInfo.InvariantCulture);
            double maximum = Convert.ToDouble(range.Maximum, CultureInfo.InvariantCulture);
            if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || maximum <= minimum)
                throw new InvalidOperationException($"{property.Name} has an invalid slider range.");

            DockPanel dockPanel = new();
            dockPanel.Children.Add(PropertyEditorHelper.CreateLabel(property, PropertyEditorHelper.GetResourceManager(obj)));

            Grid editor = new();
            editor.ColumnDefinitions.Add(new ColumnDefinition());
            editor.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Binding sliderBinding = PropertyEditorHelper.CreateTwoWayBinding(obj, property);
            Slider slider = new()
            {
                Minimum = minimum,
                Maximum = maximum,
                SmallChange = 0.01,
                LargeChange = Math.Max(0.1, (maximum - minimum) / 10),
                IsMoveToPointEnabled = true,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            slider.SetBinding(System.Windows.Controls.Primitives.RangeBase.ValueProperty, sliderBinding);
            editor.Children.Add(slider);

            Binding textBinding = PropertyEditorHelper.CreateTwoWayBinding(obj, property);
            textBinding.StringFormat = "0.0##";
            TextBox textBox = PropertyEditorHelper.CreateSmallTextBox(textBinding);
            textBox.Width = 76;
            textBox.MinHeight = 30;
            textBox.Margin = new Thickness(0);
            textBox.HorizontalContentAlignment = HorizontalAlignment.Right;
            textBox.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;
            Grid.SetColumn(textBox, 1);
            editor.Children.Add(textBox);

            dockPanel.Children.Add(editor);
            return dockPanel;
        }
    }
}
