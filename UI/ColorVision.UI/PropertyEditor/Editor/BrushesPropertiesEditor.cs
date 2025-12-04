using ColorVision.Themes;
using ColorVision.UI;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace System.ComponentModel
{
    public class BrushesPropertiesEditor : IPropertyEditor
    {
        static BrushesPropertiesEditor()
        {
            PropertyEditorHelper.RegisterEditor<BrushesPropertiesEditor>(t => typeof(Brush).IsAssignableFrom(t) || t == typeof(Color));
        }

        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();

            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);

            var button = new Button
            {
                Margin = new Thickness(5, 0, 0, 0),
                Height = 20,
                Width = 22
            };

            button.Click += (_, __) =>
            {
                var colorPicker = new HandyControl.Controls.ColorPicker();
                if (property.GetValue(obj) is SolidColorBrush scb)
                {
                    colorPicker.SelectedBrush = scb;
                    colorPicker.SelectedColorChanged += (_, __) =>
                    {
                        property.SetValue(obj, colorPicker.SelectedBrush);
                        button.Background = colorPicker.SelectedBrush;
                    };
                }
                if (property.GetValue(obj) is Color color)
                {
                    colorPicker.SelectedBrush = new SolidColorBrush(color);
                }



                var window = new Window
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = colorPicker,
                    Width = 250,
                    Height = 400
                };
                window.ApplyCaption();
                colorPicker.Confirmed += (_, __) =>
                {
                    if (property.GetValue(obj) is Color color)
                    {
                        property.SetValue(obj, colorPicker.SelectedBrush.Color);
                    }
                    else
                    {
                        property.SetValue(obj, colorPicker.SelectedBrush);
                    }
                    button.Background = colorPicker.SelectedBrush;
                    window.Close();
                };
                window.Closed += (_, __) => colorPicker.Dispose();
                window.ShowDialog();
            };
            if (property.GetValue(obj) is Color color)
            {
                button.Background = new SolidColorBrush(color);
            }
            else
            {
                var binding = new Binding(property.Name)
                {
                    Source = obj,
                    Mode = BindingMode.OneWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };
                button.SetBinding(Control.BackgroundProperty, binding);
            }


            DockPanel.SetDock(button, Dock.Right);
            dockPanel.Children.Add(button);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }
    }
}
