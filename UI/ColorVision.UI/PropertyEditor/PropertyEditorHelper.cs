using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Extension;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI
{
    public static class PropertyEditorHelper
    {
        public static ConcurrentDictionary<Type, Lazy<ResourceManager?>> ResourceManagerCache { get; set; } = new ConcurrentDictionary<Type, Lazy<ResourceManager?>>();


        public static void GenCommand(ViewModelBase obj, UniformGrid uniformGrid)
        {
            uniformGrid.SizeChanged +=(s,e) => uniformGrid.AutoUpdateLayout();

            Type type = obj.GetType();
            var lazyResourceManager = ResourceManagerCache.GetOrAdd(type, t => new Lazy<ResourceManager?>(() =>
            {
                string namespaceName = t.Assembly.GetName().Name;
                string resourceClassName = $"{namespaceName}.Properties.Resources";
                Type resourceType = t.Assembly.GetType(resourceClassName);

                if (resourceType != null)
                {
                    var resourceManagerProperty = resourceType.GetProperty("ResourceManager", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (resourceManagerProperty != null)
                    {
                        return (ResourceManager)resourceManagerProperty.GetValue(null);
                    }
                }

                return null;
            }));
            ResourceManager? resourceManager = lazyResourceManager.Value;

            var commandDisplayAttributes = new List<(CommandDisplayAttribute, PropertyInfo)>();
            var properties = type.GetProperties();
            foreach (var property in properties)
            {
                var attribute = property.GetCustomAttribute<CommandDisplayAttribute>();
                if (attribute != null)
                {
                    var browsableAttribute = property.GetCustomAttribute<BrowsableAttribute>();
                    if (browsableAttribute != null && !browsableAttribute.Browsable)
                        continue;
                    commandDisplayAttributes.Add((attribute, property));
                }
            }

            commandDisplayAttributes.Sort((a, b) => a.Item1.Order.CompareTo(b.Item1.Order));

            foreach (var (attribute, property) in commandDisplayAttributes)
            {
                var command = property.GetValue(obj) as ICommand;
                if (command != null)
                {
                    string displayName = attribute.DisplayName ?? property.Name;
                    displayName = resourceManager?.GetString(displayName, Thread.CurrentThread.CurrentUICulture) ?? displayName;
                    var button = new Button
                    {
                        Style = (Style)Application.Current.FindResource("ButtonCommand"),
                        Content = displayName,
                        Command = command
                    };
                    if (attribute.CommandType == CommandType.Highlighted)
                    {
                        button.Foreground = Brushes.Red;
                    }
                    uniformGrid.Children.Add(button);
                }
            }



        }


        public static StackPanel GenPropertyEditorControl(ViewModelBase obj)
        {
            Type type = obj.GetType();
            var lazyResourceManager = ResourceManagerCache.GetOrAdd(type, t => new Lazy<ResourceManager?>(() =>
            {
                string namespaceName = t.Assembly.GetName().Name;
                string resourceClassName = $"{namespaceName}.Properties.Resources";
                Type resourceType = t.Assembly.GetType(resourceClassName);

                if (resourceType != null)
                {
                    var resourceManagerProperty = resourceType.GetProperty("ResourceManager", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (resourceManagerProperty != null)
                    {
                        return (ResourceManager)resourceManagerProperty.GetValue(null);
                    }
                }

                return null;
            }));
            ResourceManager? resourceManager = lazyResourceManager.Value;


            void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter)
                {
                    Common.NativeMethods.Keyboard.PressKey(0x09);
                    e.Handled = true;
                }
            }
            Dictionary<string, List<PropertyInfo>> categoryGroups = new Dictionary<string, List<PropertyInfo>>();
            void GenCategoryGroups(ViewModelBase source)
            {
                Type type = source.GetType();
                var title = type.GetCustomAttribute<DisplayNameAttribute>();

                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                      .Where(p => p.CanRead && p.CanWrite);

                foreach (PropertyInfo property in properties)
                {
                    var categoryAttr = property.GetCustomAttribute<CategoryAttribute>();
                    string category = categoryAttr?.Category ?? "default";
                    if (!categoryGroups.TryGetValue(category, out List<PropertyInfo>? value))
                    {
                        categoryGroups.Add(category, new List<PropertyInfo>() { property });
                    }
                    else
                    {
                        value.Add(property);
                    }

                    //子类型如果查找不到则设置为空
                    var browsableAttr = property.GetCustomAttribute<BrowsableAttribute>();
                    if (browsableAttr?.Browsable ?? false)
                    {
                        if (property.PropertyType.IsSubclassOf(typeof(ViewModelBase)))
                        {
                            var fieldValue = property.GetValue(source);

                            if (fieldValue is ViewModelBase viewModelBase)
                            {
                                Type type1 = fieldValue.GetType();
                                GenCategoryGroups(viewModelBase);
                            }
                        }
                    }
                }
            }

            DockPanel GenBoolProperties(PropertyInfo property, object obj)
            {
                var displayNameAttr = property.GetCustomAttribute<DisplayNameAttribute>();
                var descriptionAttr = property.GetCustomAttribute<DescriptionAttribute>();

                string displayName = displayNameAttr?.DisplayName ?? property.Name;
                displayName = resourceManager?.GetString(displayName, Thread.CurrentThread.CurrentUICulture) ?? displayName;

                var dockPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
                var textBlock = new TextBlock
                {
                    Text = displayName,
                    MinWidth = 120,
                    Foreground = (Brush)Application.Current.FindResource("GlobalTextBrush")
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

            DockPanel GenEnumProperties(PropertyInfo property, object obj)
            {
                var displayNameAttr = property.GetCustomAttribute<DisplayNameAttribute>();
                var descriptionAttr = property.GetCustomAttribute<DescriptionAttribute>();
                var PropertyEditorTypeAttr = property.GetCustomAttribute<PropertyEditorTypeAttribute>();

                PropertyEditorType propertyEditorType = PropertyEditorTypeAttr?.PropertyEditorType ?? PropertyEditorType.Default;

                string displayName = displayNameAttr?.DisplayName ?? property.Name;
                displayName = resourceManager?.GetString(displayName, Thread.CurrentThread.CurrentUICulture) ?? displayName;
                var dockPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };

                var textBlock = new TextBlock
                {
                    Text = displayName,
                    MinWidth = 120,
                    Foreground = (Brush)Application.Current.FindResource("GlobalTextBrush")
                };
                dockPanel.Children.Add(textBlock);

                var comboBox = new ComboBox
                {
                    Margin = new Thickness(5, 0, 0, 0),
                    MinWidth = 150,
                    Style = (Style)Application.Current.FindResource("ComboBox.Small"),
                };

                // Populate ComboBox with Enum values
                var enumValues = Enum.GetValues(property.PropertyType);
                foreach (var value in enumValues)
                {
                    comboBox.Items.Add(value);
                }
                // Bind selected value to property
                var binding = new Binding(property.Name)
                {
                    Source = obj,
                    Mode = BindingMode.TwoWay
                };
                comboBox.SetBinding(ComboBox.SelectedItemProperty, binding);

                dockPanel.Children.Add(comboBox);
                return dockPanel;

            }

            DockPanel GenTextboxProperties(PropertyInfo property, object obj)
            {
                var displayNameAttr = property.GetCustomAttribute<DisplayNameAttribute>();
                var descriptionAttr = property.GetCustomAttribute<DescriptionAttribute>();
                var PropertyEditorTypeAttr = property.GetCustomAttribute<PropertyEditorTypeAttribute>();

                PropertyEditorType propertyEditorType = PropertyEditorTypeAttr?.PropertyEditorType ?? PropertyEditorType.Default;

                string displayName = displayNameAttr?.DisplayName ?? property.Name;
                displayName = resourceManager?.GetString(displayName, Thread.CurrentThread.CurrentUICulture) ?? displayName;
                var dockPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };

                var textBlock = new TextBlock
                {
                    Text = displayName,
                    MinWidth = 120,
                    Foreground = (Brush)Application.Current.FindResource("GlobalTextBrush")
                };
                dockPanel.Children.Add(textBlock);

                if (propertyEditorType == PropertyEditorType.TextSelectFile)
                {
                    var button = new Button
                    {
                        Content = "...",
                        Margin = new Thickness(5, 0, 0, 0)
                    };
                    button.Click += (s, e) =>
                    {
                        var openFileDialog = new Microsoft.Win32.OpenFileDialog();
                        string Filepath = (string)property.GetValue(obj);
#if NET8_0
                        ///8.0才有这个属性
                        if (File.Exists(Filepath))
                        {
                            openFileDialog.DefaultDirectory = Directory.GetDirectoryRoot(Filepath);
                        }
#endif
                        if (openFileDialog.ShowDialog() == true)
                        {
                            property.SetValue(obj, openFileDialog.FileName);
                        }
                    };

                    var textbox = new TextBox
                    {
                        Margin = new Thickness(5, 0, 0, 0),
                        Style = (Style)Application.Current.FindResource("TextBox.Small")
                    };
                    var binding = new Binding(property.Name)
                    {
                        Source = obj,
                        Mode = BindingMode.TwoWay
                    };
                    textbox.SetBinding(TextBox.TextProperty, binding);
                    DockPanel.SetDock(button, Dock.Right);
                    var button1 = new Button
                    {
                        Content = "🗁",
                        Margin = new Thickness(5, 0, 0, 0),
                    };
                    button1.Click += (s, e) =>
                    {
                        Common.Utilities.PlatformHelper.OpenFolder((string)property.GetValue(obj));
                    };
                    DockPanel.SetDock(button1, Dock.Right);
                    dockPanel.Children.Add(button1);
                    dockPanel.Children.Add(button);
                    dockPanel.Children.Add(textbox);
                }
                else if (propertyEditorType == PropertyEditorType.TextSelectFolder)
                {
                    var button = new Button
                    {
                        Content = "...",
                        Margin = new Thickness(5, 0, 0, 0)
                    };
                    button.Click += (s, e) =>
                    {
                        var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
                        folderDialog.SelectedPath = (string)property.GetValue(obj) ?? string.Empty;
                        if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            if (folderDialog.SelectedPath == null) return;
                            property.SetValue(obj, folderDialog.SelectedPath);
                        }
                    };

                    var textbox = new TextBox
                    {
                        Margin = new Thickness(5, 0, 0, 0),
                        Style = (Style)Application.Current.FindResource("TextBox.Small")
                    };
                    var binding = new Binding(property.Name)
                    {
                        Source = obj,
                        Mode = BindingMode.TwoWay
                    };
                    textbox.SetBinding(TextBox.TextProperty, binding);
                    DockPanel.SetDock(button, Dock.Right);
                    var button1 = new Button
                    {
                        Content = "🗁",
                        Margin = new Thickness(5, 0, 0, 0),
                    };
                    button1.Click += (s, e) =>
                    {
                        Common.Utilities.PlatformHelper.OpenFolder((string)property.GetValue(obj));
                    };
                    DockPanel.SetDock(button1, Dock.Right);
                    dockPanel.Children.Add(button1);
                    dockPanel.Children.Add(button);
                    dockPanel.Children.Add(textbox);

                }
                else if (propertyEditorType == PropertyEditorType.CronExpression)
                {
                    var cronButton = new Button
                    {
                        Content = "在线Cron表达式生成器",
                        Margin = new Thickness(5, 0, 0, 0),
                    };
                    DockPanel.SetDock(cronButton, Dock.Right);
                    cronButton.Click += (s, e) =>
                    {
                        PlatformHelper.Open("https://cron.qqe2.com/");
                    };
                    var cronTextBox = new TextBox
                    {
                        Margin = new Thickness(5, 0, 0, 0),
                        Style = (Style)Application.Current.FindResource("TextBox.Small")
                    };
                    var cronBinding = new Binding(property.Name)
                    {
                        Source = obj,
                        Mode = BindingMode.TwoWay
                    };
                    cronTextBox.SetBinding(TextBox.TextProperty, cronBinding);
                    dockPanel.Children.Add(cronButton);
                    dockPanel.Children.Add(cronTextBox);
                }
                else if (propertyEditorType == PropertyEditorType.TextSerialPort)
                {
                    List<string> Serials = new List<string>() { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM10", "COM11", "COM12", "COM13" , "COM14", "COM15", "COM16" };
                    HandyControl.Controls.ComboBox serialPortComboBox = new HandyControl.Controls.ComboBox
                    {
                        Margin = new Thickness(5, 0, 0, 0),
                        Style = (Style)Application.Current.FindResource("ComboBox.Small"),
                        IsEditable = true,
                    };
                    serialPortComboBox.ItemsSource = Serials;
                    var baudRateBinding = new Binding(property.Name)
                    {
                        Source = obj,
                        Mode = BindingMode.TwoWay
                    };
                    HandyControl.Controls.InfoElement.SetShowClearButton(serialPortComboBox, true);
                    serialPortComboBox.SetBinding(ComboBox.TextProperty, baudRateBinding);
                    dockPanel.Children.Add(serialPortComboBox);

                }
                else if (propertyEditorType == PropertyEditorType.TextBaudRate)
                {
                    List<int> BaudRates = new() { 115200, 9600, 300, 600, 1200, 2400, 4800, 14400, 19200, 38400, 57600 ,230400,460800,921600};
                    HandyControl.Controls.ComboBox baudRateComboBox = new HandyControl.Controls.ComboBox
                    {
                        Margin = new Thickness(5, 0, 0, 0),
                        Style = (Style)Application.Current.FindResource("ComboBox.Small"),
                        IsEditable = true,
                    };
                    baudRateComboBox.ItemsSource = BaudRates;
                    var baudRateBinding = new Binding(property.Name)
                    {
                        Source = obj,
                        Mode = BindingMode.TwoWay
                    };
                    HandyControl.Controls.InfoElement.SetShowClearButton(baudRateComboBox, true);
                    baudRateComboBox.SetBinding(ComboBox.TextProperty, baudRateBinding);
                    dockPanel.Children.Add(baudRateComboBox);
                }
                else
                {
                    var textbox = new TextBox
                    {
                        Margin = new Thickness(5, 0, 0, 0),
                        Style = (Style)Application.Current.FindResource("TextBox.Small")
                    };
                    textbox.PreviewKeyDown += TextBox_PreviewKeyDown;
                    var binding = new Binding(property.Name)
                    {
                        Source = obj,
                        Mode = BindingMode.TwoWay
                    };
                    textbox.SetBinding(TextBox.TextProperty, binding);
                    dockPanel.Children.Add(textbox);
                }
                return dockPanel;
            }

            StackPanel PropertyPanel = new StackPanel();
            GenCategoryGroups(obj);
            foreach (var categoryGroup in categoryGroups)
            {
                var border = new Border
                {
                    Background = (Brush) Application.Current.FindResource("GlobalBorderBrush"),
                    BorderThickness = new Thickness(1),
                    BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
                    CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(0, 0, 0, 5)
                };
                var stackPanel = new StackPanel { Margin = new Thickness(10, 5, 10, 5) };
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
                        else if (property.PropertyType == typeof(int) || property.PropertyType ==(typeof(float))|| property.PropertyType == (typeof(Rect)) || property.PropertyType == typeof(uint) || property.PropertyType == typeof(long) || property.PropertyType == typeof(ulong) || property.PropertyType == typeof(sbyte) || property.PropertyType == typeof(double) || property.PropertyType == typeof(string))
                        {
                            dockPanel = GenTextboxProperties(property, obj);
                        }
                        else if (property.PropertyType.IsEnum)
                        {
                            dockPanel = GenEnumProperties(property, obj);
                        }
                        else if (typeof(ViewModelBase).IsAssignableFrom(property.PropertyType))
                        {
                            // 如果属性是ViewModelBase的子类，递归解析
                            var nestedObj = (ViewModelBase)property.GetValue(obj);
                            if (nestedObj != null)
                            {
                                stackPanel.Children.Add(GenPropertyEditorControl(nestedObj));
                                continue;
                            }
                        }
                        if (categoryGroup.Value.IndexOf(property) == categoryGroup.Value.Count - 1)
                        {
                            dockPanel.Margin = new Thickness(0);
                        }

                        var VisibleBlindAttr = property.GetCustomAttribute<PropertyVisibilityAttribute>();
                        if (VisibleBlindAttr != null)
                        {
                            var binding = new Binding(VisibleBlindAttr.PropertyName)
                            {
                                Source = obj,
                                Mode = BindingMode.TwoWay
                            };
                            binding.Converter = (IValueConverter)Application.Current.FindResource("bool2VisibilityConverter");
                            dockPanel.SetBinding(DockPanel.VisibilityProperty, binding);


                        }
                        stackPanel.Children.Add(dockPanel);
                    }
                }
            }

            return PropertyPanel;

        }


    }
}
