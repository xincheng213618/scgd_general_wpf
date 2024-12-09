using ColorVision.Common.MVVM;
using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.UI.PropertyEditor
{
    /// <summary>
    /// EditConfig.xaml 的交互逻辑
    /// </summary>
    public partial class PropertyEditorWindow : Window
    {
        public ViewModelBase Config { get; set; }
        public PropertyEditorWindow(ViewModelBase config)
        {
            Config = config;
            InitializeComponent();
            this.ApplyCaption();
        }
        public Dictionary<string, List<PropertyInfo>> categoryGroups { get; set; } = new Dictionary<string, List<PropertyInfo>>();

        public void DisplayProperties(ViewModelBase obj)
        {
            Type type = obj.GetType();
            PropertyInfo[] properties = type.GetProperties();
            foreach (PropertyInfo property in properties)
            {
                var categoryAttr = property.GetCustomAttribute<CategoryAttribute>();
                string category = categoryAttr?.Category ?? "Default";
                if (!categoryGroups.TryGetValue(category, out List<PropertyInfo>? value))
                {
                    categoryGroups.Add(category, new List<PropertyInfo>() { property });
                }
                else
                {
                    value.Add(property);
                }
            }

            foreach (var categoryGroup in categoryGroups)
            {
                var border = new Border
                {
                    Background = (Brush)FindResource("GlobalBorderBrush"),
                    BorderThickness = new Thickness(1),
                    BorderBrush = (Brush)FindResource("BorderBrush"),
                    CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(0, 0, 0, 5)
                };
                var stackPanel = new StackPanel { Margin = new Thickness(10, 5,10,5) };
                border.Child = stackPanel;
                PropertyPanel.Children.Add(border);

                foreach (var property in categoryGroup.Value)
                {
                    var browsableAttr = property.GetCustomAttribute<BrowsableAttribute>();
                    if (browsableAttr?.Browsable ?? true)
                    {
                        DockPanel dockPanel = new DockPanel();
                        if (property.PropertyType == typeof(bool))
                        {
                            dockPanel = GenBoolProperties(property, obj);
                        }
                        else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(double) || property.PropertyType == typeof(string))
                        {
                            dockPanel = GenTextboxProperties(property, obj);
                        }
                        if (categoryGroup.Value.IndexOf(property) == categoryGroup.Value.Count - 1)
                        {
                            dockPanel.Margin = new Thickness(0);
                        }
                        stackPanel.Children.Add(dockPanel);
                    }

                }
            }



        }

        

        public DockPanel GenBoolProperties(PropertyInfo property,object obj)
        {
            var displayNameAttr = property.GetCustomAttribute<DisplayNameAttribute>();
            var descriptionAttr = property.GetCustomAttribute<DescriptionAttribute>();

            string displayName = displayNameAttr?.DisplayName ?? property.Name;
            displayName = Properties.Resources.ResourceManager.GetString(displayName, CultureInfo.CurrentCulture) ?? displayName;

            var dockPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
            var textBlock = new TextBlock
            {
                Text = displayName,
                MinWidth = 120
            };
            dockPanel.Children.Add(textBlock);

            var toggleSwitch = new Wpf.Ui.Controls.ToggleSwitch
            {
                Margin = new Thickness(5, 0, 0, 0),
            };
            var binding = new Binding(property.Name)
            {
                Source = obj,
                Mode = BindingMode.TwoWay
            };
            toggleSwitch.SetBinding(ToggleButton.IsCheckedProperty, binding);
            dockPanel.Children.Add(toggleSwitch);
            return dockPanel;
        }

        public DockPanel GenTextboxProperties(PropertyInfo property, object obj)
        {
            var displayNameAttr = property.GetCustomAttribute<DisplayNameAttribute>();
            var descriptionAttr = property.GetCustomAttribute<DescriptionAttribute>();

            string displayName = displayNameAttr?.DisplayName ?? property.Name;
            displayName = Properties.Resources.ResourceManager.GetString(displayName, CultureInfo.CurrentCulture) ?? displayName;

            var dockPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
            var textBlock = new TextBlock
            {
                Text = displayName,
                MinWidth = 120
            };
            dockPanel.Children.Add(textBlock);

            var textbox = new TextBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                Style = (Style)FindResource("TextBox.Small")
            };

            var binding = new Binding(property.Name)
            {
                Source = obj,
                Mode = BindingMode.TwoWay
            };
            textbox.SetBinding(TextBox.TextProperty, binding);
            dockPanel.Children.Add(textbox);
            return dockPanel;
        }


        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            DisplayProperties(Config);
        }
    }
}
