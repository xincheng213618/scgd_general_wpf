#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.PropertyEditor
{
    /// <summary>
    /// EditConfig.xaml 的交互逻辑
    /// </summary>
    public partial class PropertyEditorWindow : Window
    {
        public event EventHandler Submited;
        public ViewModelBase Config { get; set; }
        public ViewModelBase EditConfig { get; set; }

        public bool IsEdit { get; set; } = true;

        public PropertyEditorWindow(ViewModelBase config ,bool isEdit = true)
        {
            IsEdit = isEdit;
            Config = config;
            InitializeComponent();
            this.ApplyCaption();
        }
        public Dictionary<string, List<PropertyInfo>> categoryGroups { get; set; } = new Dictionary<string, List<PropertyInfo>>();

        public void GenCategoryGroups(ViewModelBase source)
        {
            Type type = source.GetType();
            var title = type.GetCustomAttribute<DisplayNameAttribute>();
            if (title != null)
                this.Title = title.DisplayName;

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


        public void DisplayProperties(ViewModelBase obj)
        {
            categoryGroups.Clear();
            GenCategoryGroups(obj);
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
                        else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(uint) || property.PropertyType == typeof(long) || property.PropertyType == typeof(ulong) || property.PropertyType == typeof(sbyte) || property.PropertyType == typeof(double) || property.PropertyType == typeof(string))
                        {
                            dockPanel = GenTextboxProperties(property, obj);
                        }
                        else if (property.PropertyType.IsEnum)
                        {
                            dockPanel = GenEnumProperties(property, obj);

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
                MinWidth = 120,
                Foreground = (Brush)FindResource("GlobalTextBrush")
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


        public DockPanel GenEnumProperties(PropertyInfo property, object obj)
        {
            var displayNameAttr = property.GetCustomAttribute<DisplayNameAttribute>();
            var descriptionAttr = property.GetCustomAttribute<DescriptionAttribute>();
            var PropertyEditorTypeAttr = property.GetCustomAttribute<PropertyEditorTypeAttribute>();

            PropertyEditorType propertyEditorType = PropertyEditorTypeAttr?.PropertyEditorType ?? PropertyEditorType.Default;

            string displayName = displayNameAttr?.DisplayName ?? property.Name;
            displayName = Properties.Resources.ResourceManager.GetString(displayName, CultureInfo.CurrentCulture) ?? displayName;
            var dockPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };

            var textBlock = new TextBlock
            {
                Text = displayName,
                MinWidth = 120,
                Foreground = (Brush)FindResource("GlobalTextBrush")
            };
            dockPanel.Children.Add(textBlock);

            var comboBox = new ComboBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = 150
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

        public DockPanel GenTextboxProperties(PropertyInfo property, object obj)
        {
            var displayNameAttr = property.GetCustomAttribute<DisplayNameAttribute>();
            var descriptionAttr = property.GetCustomAttribute<DescriptionAttribute>();
            var PropertyEditorTypeAttr = property.GetCustomAttribute<PropertyEditorTypeAttribute>();

            PropertyEditorType propertyEditorType = PropertyEditorTypeAttr?.PropertyEditorType ?? PropertyEditorType.Default;

            string displayName = displayNameAttr?.DisplayName ?? property.Name;
            displayName = Properties.Resources.ResourceManager.GetString(displayName, CultureInfo.CurrentCulture) ?? displayName;
            var dockPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };

            var textBlock = new TextBlock
            {
                Text = displayName,
                MinWidth = 120,
                Foreground = (Brush)FindResource("GlobalTextBrush")
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
                    Style = (Style)FindResource("TextBox.Small")
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
                    folderDialog.SelectedPath = (string)property.GetValue(obj);
                    if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        if (folderDialog.SelectedPath == null) return;
                        property.SetValue(obj, folderDialog.SelectedPath);
                    }
                };

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
                    Style = (Style)FindResource("TextBox.Small")
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
            else
            {
                var textbox = new TextBox
                {
                    Margin = new Thickness(5, 0, 0, 0),
                    Style = (Style)FindResource("TextBox.Small")
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
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            if (IsEdit)
            {
                DisplayProperties(Config);
            }
            else
            {
                EditConfig = Config.Clone();
                DisplayProperties(EditConfig);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (!IsEdit)
                EditConfig.CopyTo(Config);
            Submited?.Invoke(sender, new EventArgs());
            this.Close();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (!IsEdit)
            {
                Config.CopyTo(EditConfig);
                PropertyPanel.Children.Clear();
                DisplayProperties(EditConfig);
            }
            else
            {
                Config.Reset();
            }
        }
    }
}
