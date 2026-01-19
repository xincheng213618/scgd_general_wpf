using ColorVision.Engine.Services;
using ColorVision.UI;
using ICSharpCode.AvalonEdit.Document;
using System;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.PropertyEditor
{
    [AttributeUsage(AttributeTargets.Property)]
    public class DeviceSourceTypeAttribute : Attribute
    {
        public Type DeviceType { get; }
        public DeviceSourceTypeAttribute(Type deviceType)
        {
            DeviceType = deviceType;
        }
    }

    public class DeviceNameEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);

            var combo = new HandyControl.Controls.ComboBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                Style = PropertyEditorHelper.ComboBoxSmallStyle,
                IsEditable = true
            };
            HandyControl.Controls.InfoElement.SetShowClearButton(combo, true);
            combo.SetBinding(ComboBox.TextProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name));


            var sourceTypeAttr = property.GetCustomAttribute<DeviceSourceTypeAttribute>();
            Type targetType = sourceTypeAttr?.DeviceType ?? typeof(object); // 默认为 object 或你的设备基类

            var ItemsSource = ServiceManager.GetInstance().DeviceServices.Where(d => targetType.IsInstanceOfType(d)).ToList();

            combo.ItemsSource = ItemsSource;
            combo.DisplayMemberPath = "Name";


            string? code = property.GetValue(obj).ToString();
            var selectedItem = ItemsSource.FirstOrDefault(x => x.Name == code);
            if (selectedItem != null)
                combo.SelectedIndex = ItemsSource.IndexOf(selectedItem);

            Grid myGrid = new Grid();
            myGrid.DataContext = selectedItem;

            combo.SelectionChanged += (s, e) =>  myGrid.DataContext = combo.SelectedValue;


            var button = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };

            var toggleButton = new ToggleButton
            {
                Style = (Style)Application.Current.FindResource("ButtonMQTTConnect"),
                Height = 10,
                Width = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsEnabled = false
            };
            // Create the binding for IsChecked
            var binding = new Binding("DService.IsAlive")
            {
                Mode = BindingMode.OneWay
            };
            // Set the binding to the ToggleButton
            toggleButton.SetBinding(ToggleButton.IsCheckedProperty, binding);
            // Create an Image
            var image = new Image
            {
                Source = (ImageSource)Application.Current.FindResource("DrawingImageProperty"),
                Height = 18,
                Margin = new Thickness(0)
            };
            // Create the binding for IsChecked
            var binding1 = new Binding("PropertyCommand")
            {
                Mode = BindingMode.OneWay
            };
            // Set the binding to the ToggleButton
            button.SetBinding(Button.CommandProperty, binding1);

            // Add elements to the Grid
            myGrid.Children.Add(toggleButton);
            myGrid.Children.Add(image);
            myGrid.Children.Add(button);

            DockPanel.SetDock(myGrid,Dock.Right);
            dockPanel.Children.Add(myGrid);

            dockPanel.Children.Add(textBlock);
            dockPanel.Children.Add(combo);
            return dockPanel;
        }
    }

    public class TextSerialPortPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            string[] SerialPorts = SerialPort.GetPortNames();
            var combo = new HandyControl.Controls.ComboBox { Margin = new Thickness(5, 0, 0, 0), Style = PropertyEditorHelper.ComboBoxSmallStyle, IsEditable = true, ItemsSource = SerialPorts };
            HandyControl.Controls.InfoElement.SetShowClearButton(combo, true);
            combo.SetBinding(ComboBox.TextProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name));
            dockPanel.Children.Add(combo);
            return dockPanel;
        }
    }
}
