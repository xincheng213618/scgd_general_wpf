using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Extension;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ColorVision.UI
{
    public static class PropertyEditorHelper
    {
        // Constants
        public const double LabelMinWidth = 120;
        public const double ControlMinWidth = 150;

        // Cache for resources and reflection results
        public static ConcurrentDictionary<Type, Lazy<ResourceManager?>> ResourceManagerCache { get; set; } = new();

        // Cached resource lookups per app lifetime (lookups are cheap but repeated hundreds of times in dynamic editors)
        public static Brush GlobalTextBrush => (Brush)Application.Current.FindResource("GlobalTextBrush");
        public static Brush GlobalBorderBrush => (Brush)Application.Current.FindResource("GlobalBorderBrush");
        public static Brush BorderBrush => (Brush)Application.Current.FindResource("BorderBrush");
        public static Style ButtonCommandStyle => (Style)Application.Current.FindResource("ButtonCommand");
        public static Style ComboBoxSmallStyle => (Style)Application.Current.FindResource("ComboBox.Small");
        public static Style TextBoxSmallStyle => (Style)Application.Current.FindResource("TextBox.Small");
        public static IValueConverter? Bool2VisibilityConverter => Application.Current.TryFindResource("bool2VisibilityConverter") as IValueConverter;

        public static ResourceManager? GetResourceManager(object obj)
        {
            var type = obj.GetType();
            var lazyResourceManager = ResourceManagerCache.GetOrAdd(type, t => new Lazy<ResourceManager?>(() =>
            {
                try
                {
                    string namespaceName = t.Assembly.GetName().Name!;
                    string resourceClassName = $"{namespaceName}.Properties.Resources";
                    Type? resourceType = t.Assembly.GetType(resourceClassName);
                    if (resourceType != null)
                    {
                        var rmProp = resourceType.GetProperty("ResourceManager", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        if (rmProp?.GetValue(null) is ResourceManager rm)
                            return rm;
                    }
                }
                catch
                {
                    // ignore and fallback to null
                }
                return null;
            }));
            return lazyResourceManager.Value;
        }

        public static void GenCommand(object obj, UniformGrid uniformGrid)
        {
            if (uniformGrid == null) return;
            uniformGrid.SizeChanged += (_, __) => uniformGrid.AutoUpdateLayout();

            var type = obj.GetType();
            ResourceManager? rm = GetResourceManager(obj);

            var commands = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => (Prop: p, Attr: p.GetCustomAttribute<CommandDisplayAttribute>(),
                              Browsable: p.GetCustomAttribute<BrowsableAttribute>()))
                .Where(x => x.Attr != null && (x.Browsable?.Browsable ?? true))
                .OrderBy(x => x.Attr!.Order)
                .ToList();

            foreach (var item in commands)
            {
                if (item.Prop.GetValue(obj) is not ICommand command) continue;

                string displayName = GetDisplayName(rm, item.Prop, item.Attr!.DisplayName);
                var button = new Button
                {
                    Style = ButtonCommandStyle,
                    Content = displayName,
                    Command = command,
                    ToolTip = displayName
                };
                if (item.Attr!.CommandType == CommandType.Highlighted)
                {
                    button.Foreground = Brushes.Red;
                }
                uniformGrid.Children.Add(button);
            }
        }

        public static void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Use WPF focus traversal instead of simulating a tab key press
            if (e.Key == Key.Enter)
            {
                if (sender is UIElement uie)
                {
                    uie.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    e.Handled = true;
                }
            }
        }

        public static DockPanel GenBoolProperties(PropertyInfo property, object obj)
        {
            var rm = GetResourceManager(obj);
            var dockPanel = new DockPanel();

            var textBlock = CreateLabel(property, rm);
            var toggleSwitch = new Wpf.Ui.Controls.ToggleSwitch
            {
                Margin = new Thickness(5, 0, 0, 0),
            };
            var binding = CreateTwoWayBinding(obj, property.Name);
            toggleSwitch.SetBinding(ToggleButton.IsCheckedProperty, binding);
            DockPanel.SetDock(toggleSwitch, Dock.Right);

            dockPanel.Children.Add(toggleSwitch);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }
        public static ComboBox GenEnumPropertiesComboBox(PropertyInfo property, object obj)
        {
            var rm = GetResourceManager(obj);
            var comboBox = new ComboBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = ControlMinWidth,
                Style = ComboBoxSmallStyle,
                ItemsSource = Enum.GetValues(property.PropertyType)
            };

            var binding = CreateTwoWayBinding(obj, property.Name);
            comboBox.SetBinding(Selector.SelectedItemProperty, binding);
            return comboBox;
        }


        public static DockPanel GenEnumProperties(PropertyInfo property, object obj)
        {
            var rm = GetResourceManager(obj);
            var dockPanel = new DockPanel();

            var textBlock = CreateLabel(property, rm);
            var comboBox = new ComboBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = ControlMinWidth,
                Style = ComboBoxSmallStyle,
                ItemsSource = Enum.GetValues(property.PropertyType)
            };

            var binding = CreateTwoWayBinding(obj, property.Name);
            comboBox.SetBinding(Selector.SelectedItemProperty, binding);
            DockPanel.SetDock(comboBox, Dock.Right);

            dockPanel.Children.Add(comboBox);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }



        public static DockPanel GenTextboxProperties(PropertyInfo property, object obj)
        {
            var rm = GetResourceManager(obj);
            var editorAttr = property.GetCustomAttribute<PropertyEditorTypeAttribute>();
            var editorType = editorAttr?.PropertyEditorType ?? PropertyEditorType.Default;

            var dockPanel = new DockPanel();
            var textBlock = CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            switch (editorType)
            {
                case PropertyEditorType.TextSelectFile:
                    {
                        var textBinding = CreateTwoWayBinding(obj, property.Name);

                        var textbox = CreateSmallTextBox(textBinding);
                        var selectBtn = new Button
                        {
                            Content = "...",
                            Margin = new Thickness(5, 0, 0, 0)
                        };
                        selectBtn.Click += (_, __) =>
                        {
                            var ofd = new Microsoft.Win32.OpenFileDialog();
                            var path = property.GetValue(obj) as string;
#if NET8_0
                            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                            {
                                ofd.DefaultDirectory = Directory.GetDirectoryRoot(path);
                            }
#endif
                            if (ofd.ShowDialog() == true)
                            {
                                property.SetValue(obj, ofd.FileName);
                            }
                        };

                        var openFolderBtn = new Button
                        {
                            Content = "🗁",
                            Margin = new Thickness(5, 0, 0, 0),
                            ToolTip = "打开所在文件夹"
                        };
                        openFolderBtn.Click += (_, __) =>
                        {
                            var path = property.GetValue(obj) as string;
                            if (!string.IsNullOrWhiteSpace(path))
                            {
                                PlatformHelper.OpenFolder(path);
                            }
                        };

                        DockPanel.SetDock(selectBtn, Dock.Right);
                        DockPanel.SetDock(openFolderBtn, Dock.Right);

                        dockPanel.Children.Add(openFolderBtn);
                        dockPanel.Children.Add(selectBtn);
                        dockPanel.Children.Add(textbox);
                    }
                    break;

                case PropertyEditorType.TextSelectFolder:
                    {
                        var textBinding = CreateTwoWayBinding(obj, property.Name);
                        var textbox = CreateSmallTextBox(textBinding);

                        var selectBtn = new Button
                        {
                            Content = "...",
                            Margin = new Thickness(5, 0, 0, 0)
                        };
                        selectBtn.Click += (_, __) =>
                        {
                            using var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                            {
                                SelectedPath = property.GetValue(obj) as string ?? string.Empty
                            };
                            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                            {
                                if (!string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
                                {
                                    property.SetValue(obj, folderDialog.SelectedPath);
                                }
                            }
                        };

                        var openFolderBtn = new Button
                        {
                            Content = "🗁",
                            Margin = new Thickness(5, 0, 0, 0),
                            ToolTip = "打开文件夹"
                        };
                        openFolderBtn.Click += (_, __) =>
                        {
                            var path = property.GetValue(obj) as string;
                            if (!string.IsNullOrWhiteSpace(path))
                            {
                                PlatformHelper.OpenFolder(path);
                            }
                        };

                        DockPanel.SetDock(selectBtn, Dock.Right);
                        DockPanel.SetDock(openFolderBtn, Dock.Right);

                        dockPanel.Children.Add(openFolderBtn);
                        dockPanel.Children.Add(selectBtn);
                        dockPanel.Children.Add(textbox);
                    }
                    break;

                case PropertyEditorType.TextJson:
                    {
                        var editCmd = new RelayCommand(_ =>
                        {
                            var owner = Application.Current.GetActiveWindow();
                            var wnd = new AvalonEditWindow
                            {
                                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                                Owner = owner
                            };
                            wnd.SetJsonText(property.GetValue(obj) as string ?? string.Empty);
                            wnd.Closing += (_, __) => property.SetValue(obj, wnd.GetJsonText());
                            wnd.ShowDialog();
                        });

                        var iconBtn = CreateIconSpinButton(editCmd);

                        var binding = CreateTwoWayBinding(obj, property.Name);
                        var textbox = CreateSmallTextBox(binding);

                        DockPanel.SetDock(iconBtn, Dock.Right);
                        dockPanel.Children.Add(iconBtn);
                        dockPanel.Children.Add(textbox);
                    }
                    break;

                case PropertyEditorType.CronExpression:
                    {
                        var cronBtn = new Button
                        {
                            Content = "在线Cron表达式生成器",
                            Margin = new Thickness(5, 0, 0, 0),
                            ToolTip = "打开在线Cron表达式生成器"
                        };
                        DockPanel.SetDock(cronBtn, Dock.Right);
                        cronBtn.Click += (_, __) => PlatformHelper.Open("https://cron.qqe2.com/");

                        var cronTextBox = CreateSmallTextBox(CreateTwoWayBinding(obj, property.Name));

                        dockPanel.Children.Add(cronBtn);
                        dockPanel.Children.Add(cronTextBox);
                    }
                    break;

                case PropertyEditorType.TextSerialPort:
                    {
                        // Prefer real ports if available; fallback to common names
                        //var ports = System.IO.Ports.SerialPort.GetPortNames();
                        //var serials = ports is { Length: > 0 }
                        //    ? ports.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList()
                        //    : new List<string> { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM10", "COM11", "COM12", "COM13", "COM14", "COM15", "COM16" };

                        var serials =  new List<string> { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM10", "COM11", "COM12", "COM13", "COM14", "COM15", "COM16" };

                        var combo = new HandyControl.Controls.ComboBox
                        {
                            Margin = new Thickness(5, 0, 0, 0),
                            Style = ComboBoxSmallStyle,
                            IsEditable = true,
                            ItemsSource = serials
                        };
                        HandyControl.Controls.InfoElement.SetShowClearButton(combo, true);
                        combo.SetBinding(ComboBox.TextProperty, CreateTwoWayBinding(obj, property.Name));

                        dockPanel.Children.Add(combo);
                    }
                    break;

                case PropertyEditorType.TextBaudRate:
                    {
                        var baudRates = new List<int> { 921600, 460800, 230400, 115200, 57600, 38400, 19200, 14400, 9600, 4800, 2400, 1200, 600, 300 };
                        var combo = new HandyControl.Controls.ComboBox
                        {
                            Margin = new Thickness(5, 0, 0, 0),
                            Style = ComboBoxSmallStyle,
                            IsEditable = true,
                            ItemsSource = baudRates
                        };
                        HandyControl.Controls.InfoElement.SetShowClearButton(combo, true);
                        combo.SetBinding(ComboBox.TextProperty, CreateTwoWayBinding(obj, property.Name));

                        dockPanel.Children.Add(combo);
                    }
                    break;

                default:
                    {
                        Binding binding = CreateTwoWayBinding(obj, property.Name);
                        binding.UpdateSourceTrigger = editorAttr?.UpdateSourceTrigger??UpdateSourceTrigger.Default;

                        var t = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                        if (t == typeof(float) || property.PropertyType == typeof(double))
                        {
                            binding.StringFormat = "0.0################";
                        }

                        var textbox = CreateSmallTextBox(binding);



                        textbox.PreviewKeyDown += TextBox_PreviewKeyDown;
                        dockPanel.Children.Add(textbox);
                    }
                    break;
            }

            return dockPanel;
        }

        public static DockPanel GenBrushProperties(PropertyInfo property, object obj)
        {
            var rm = GetResourceManager(obj);
            var dockPanel = new DockPanel();

            var textBlock = CreateLabel(property, rm);

            var button = new Button
            {
                Margin = new Thickness(5, 0, 0, 0),
                Height = 20,
                Width = 22
            };

            // Only attach color picker for SolidColorBrush (can be extended)

            button.Click += (_, __) =>
            {
                var colorPicker = new HandyControl.Controls.ColorPicker();
                if (property.GetValue(obj) is SolidColorBrush scb)
                {
                    colorPicker.SelectedBrush = scb;
                }

                colorPicker.SelectedColorChanged += (_, __) =>
                {
                    property.SetValue(obj, colorPicker.SelectedBrush);
                    button.Background = colorPicker.SelectedBrush;
                };

                var window = new Window
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = colorPicker,
                    Width = 250,
                    Height = 400
                };

                colorPicker.Confirmed += (_, __) =>
                {
                    property.SetValue(obj, colorPicker.SelectedBrush);
                    button.Background = colorPicker.SelectedBrush;
                    window.Close();
                };
                window.Closed += (_, __) => colorPicker.Dispose();
                window.Show();
            };

            var binding = CreateTwoWayBinding(obj, property.Name);
            button.SetBinding(Control.BackgroundProperty, binding);

            DockPanel.SetDock(button, Dock.Right);
            dockPanel.Children.Add(button);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }

        public static DockPanel GenCommandProperties(PropertyInfo property, object obj)
        {
            var rm = GetResourceManager(obj);
            var dockPanel = new DockPanel();

            var textBlock = CreateLabel(property, rm);
            var command = property.GetValue(obj) as ICommand;

            var button = new Button
            {
                Margin = new Thickness(5, 0, 0, 0),
                Content = "执行",
                Command = command
            };

            DockPanel.SetDock(button, Dock.Right);
            dockPanel.Children.Add(button);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }

        public static StackPanel GenPropertyEditorControl(object obj)
        {
            var categoryGroups = new Dictionary<string, List<PropertyInfo>>(StringComparer.Ordinal);

            void CollectProperties(object source)
            {
                var t = source.GetType();

                var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                             .Where(p => p.CanRead && p.CanWrite);

                foreach (var prop in props)
                {
                    var browsableAttr = prop.GetCustomAttribute<BrowsableAttribute>();
                    if (!(browsableAttr?.Browsable ?? true))
                        continue;

                    var categoryAttr = prop.GetCustomAttribute<CategoryAttribute>();
                    string category = categoryAttr?.Category ?? "default";

                    if (!categoryGroups.TryGetValue(category, out var list))
                    {
                        list = new List<PropertyInfo>();
                        categoryGroups[category] = list;
                    }
                    list.Add(prop);

                    try
                    {
                        // If nested ViewModelBase, recurse
                        if (typeof(ViewModelBase).IsAssignableFrom(prop.PropertyType))
                        {
                            if (prop.GetValue(source) is ViewModelBase nestedVm)
                            {
                                CollectProperties(nestedVm);
                            }
                        }
                    }
                    catch(Exception ex)
                    {

                    }


                }
            }

            var propertyPanel = new StackPanel();
            CollectProperties(obj);
            // 选择是否排序类别
            // Sort categories and properties (by display name if available)
            foreach (var categoryGroup in categoryGroups)
            {
                var border = new Border
                {
                    Background = GlobalBorderBrush,
                    BorderThickness = new Thickness(1),
                    BorderBrush = BorderBrush,
                    CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(0, 0, 0, 5)
                };
                var stackPanel = new StackPanel { Margin = new Thickness(5, 5, 5, 0) };
                border.Child = stackPanel;
                propertyPanel.Children.Add(border);

                foreach (var property in categoryGroup.Value)
                {
                    DockPanel dockPanel;

                    if (property.PropertyType.IsEnum)
                    {
                        dockPanel = GenEnumProperties(property, obj);
                    }
                    else if (property.PropertyType == typeof(bool))
                    {
                        dockPanel = GenBoolProperties(property, obj);
                    }
                    else if (IsTextEditableType(property.PropertyType))
                    {
                        dockPanel = GenTextboxProperties(property, obj);
                    }
                    else if (typeof(Brush).IsAssignableFrom(property.PropertyType))
                    {
                        dockPanel = GenBrushProperties(property, obj);
                    }
                    else if (property.PropertyType == typeof(FontFamily))
                        dockPanel = PropertyEditorHelper.GenFontFamilyProperties(property, obj);
                    else if (property.PropertyType == typeof(FontWeight))
                        dockPanel = PropertyEditorHelper.GenFontWeightProperties(property, obj);
                    else if (property.PropertyType == typeof(FontStyle))
                        dockPanel = PropertyEditorHelper.GenFontStyleProperties(property, obj);
                    else if (property.PropertyType == typeof(FontStretch))
                        dockPanel = PropertyEditorHelper.GenFontStretchProperties(property, obj);
                    else if (property.PropertyType == typeof(FlowDirection))
                        dockPanel = PropertyEditorHelper.GenFlowDirectionProperties(property, obj);
                    else if (typeof(ICommand).IsAssignableFrom(property.PropertyType))
                    {
                        dockPanel = GenCommandProperties(property, obj);
                    }
                    else if (typeof(ViewModelBase).IsAssignableFrom(property.PropertyType))
                    {
                        if (property.GetValue(obj) is ViewModelBase nested)
                        {
                            stackPanel.Children.Add(GenPropertyEditorControl(nested));
                        }
                        continue;
                    }
                    else
                    {
                        continue;
                    }

                    dockPanel.Margin = new Thickness(0, 0, 0, 5);

                    // Visibility binding based on PropertyVisibilityAttribute
                    var visibleAttr = property.GetCustomAttribute<PropertyVisibilityAttribute>();
                    if (visibleAttr != null && Bool2VisibilityConverter != null)
                    {
                        var vb = new Binding(visibleAttr.PropertyName)
                        {
                            Source = obj,
                            Mode = BindingMode.OneWay,
                            Converter = Bool2VisibilityConverter
                        };
                        dockPanel.SetBinding(UIElement.VisibilityProperty, vb);
                    }

                    stackPanel.Children.Add(dockPanel);
                }
            }

            return propertyPanel;
        }


        public static DockPanel GenFontFamilyProperties(PropertyInfo property, object obj)
        {
            var rm = GetResourceManager(obj);
            var dockPanel = new DockPanel();

            var textBlock = CreateLabel(property, rm);
            var comboBox = new ComboBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = ControlMinWidth,
                Style = ComboBoxSmallStyle,
                DisplayMemberPath = "Value",
                SelectedValuePath = "Key",
                ItemsSource = Fonts.SystemFontFamilies
                    .Select(f => new KeyValuePair<FontFamily, string>(
                        f,
                        f.FamilyNames.TryGetValue(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.Name), out string fontName) ? fontName : f.Source
                    )).ToList()
            };

            var binding = CreateTwoWayBinding(obj, property.Name);
            comboBox.SetBinding(ComboBox.SelectedValueProperty, binding);
            DockPanel.SetDock(comboBox, Dock.Right);

            dockPanel.Children.Add(comboBox);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }

        public static DockPanel GenFontWeightProperties(PropertyInfo property, object obj)
        {
            var rm = GetResourceManager(obj);
            var dockPanel = new DockPanel();

            var textBlock = CreateLabel(property, rm);
            var comboBox = new ComboBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = ControlMinWidth,
                Style = ComboBoxSmallStyle,
                DisplayMemberPath = "Value",
                SelectedValuePath = "Key",
                ItemsSource = typeof(FontWeights).GetProperties()
                    .Select(p => new KeyValuePair<FontWeight, string>((FontWeight)p.GetValue(null), p.Name)).ToList()
            };

            var binding = CreateTwoWayBinding(obj, property.Name);
            comboBox.SetBinding(ComboBox.SelectedValueProperty, binding);
            DockPanel.SetDock(comboBox, Dock.Right);

            dockPanel.Children.Add(comboBox);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }

        // FontStyle
        public static DockPanel GenFontStyleProperties(PropertyInfo property, object obj)
        {
            var rm = GetResourceManager(obj);
            var dockPanel = new DockPanel();

            var textBlock = CreateLabel(property, rm);
            var comboBox = new ComboBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = ControlMinWidth,
                Style = ComboBoxSmallStyle,
                DisplayMemberPath = "Value",
                SelectedValuePath = "Key",
                ItemsSource = typeof(FontStyles).GetProperties()
                    .Select(p => new KeyValuePair<FontStyle, string>((FontStyle)p.GetValue(null), p.Name)).ToList()
            };

            var binding = CreateTwoWayBinding(obj, property.Name);
            comboBox.SetBinding(ComboBox.SelectedValueProperty, binding);
            DockPanel.SetDock(comboBox, Dock.Right);

            dockPanel.Children.Add(comboBox);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }

        // FontStretch
        public static DockPanel GenFontStretchProperties(PropertyInfo property, object obj)
        {
            var rm = GetResourceManager(obj);
            var dockPanel = new DockPanel();

            var textBlock = CreateLabel(property, rm);
            var comboBox = new ComboBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = ControlMinWidth,
                Style = ComboBoxSmallStyle,
                DisplayMemberPath = "Value",
                SelectedValuePath = "Key",
                ItemsSource = typeof(FontStretches).GetProperties()
                    .Select(p => new KeyValuePair<FontStretch, string>((FontStretch)p.GetValue(null), p.Name)).ToList()
            };

            var binding = CreateTwoWayBinding(obj, property.Name);
            comboBox.SetBinding(ComboBox.SelectedValueProperty, binding);
            DockPanel.SetDock(comboBox, Dock.Right);

            dockPanel.Children.Add(comboBox);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }

        // FlowDirection
        public static DockPanel GenFlowDirectionProperties(PropertyInfo property, object obj)
        {
            var rm = GetResourceManager(obj);
            var dockPanel = new DockPanel();

            var textBlock = CreateLabel(property, rm);
            var comboBox = new ComboBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = ControlMinWidth,
                Style = ComboBoxSmallStyle,
                DisplayMemberPath = "Value",
                SelectedValuePath = "Key",
                ItemsSource = Enum.GetValues(typeof(FlowDirection))
                    .Cast<FlowDirection>()
                    .Select(f => new KeyValuePair<FlowDirection, string>(f, f.ToString())).ToList()
            };

            var binding = CreateTwoWayBinding(obj, property.Name);
            comboBox.SetBinding(ComboBox.SelectedValueProperty, binding);
            DockPanel.SetDock(comboBox, Dock.Right);

            dockPanel.Children.Add(comboBox);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }


        // Helpers

        private static string GetDisplayName(ResourceManager? rm, PropertyInfo prop, string? overrideName = null)
        {
            var displayNameAttr = prop.GetCustomAttribute<DisplayNameAttribute>();
            var raw = overrideName ?? displayNameAttr?.DisplayName ?? prop.Name;
            return rm?.GetString(raw, Thread.CurrentThread.CurrentUICulture) ?? raw;
        }

        private static TextBlock CreateLabel(PropertyInfo property, ResourceManager? rm)
        {
            var desc = property.GetCustomAttribute<DescriptionAttribute>()?.Description;
            var tb = new TextBlock
            {
                Text = GetDisplayName(rm, property),
                MinWidth = LabelMinWidth,
                Foreground = GlobalTextBrush,
                ToolTip = string.IsNullOrWhiteSpace(desc) ? null : desc
            };
            return tb;
        }

        private static Binding CreateTwoWayBinding(object source, string path)
        {
            return new Binding(path)
            {
                Source = source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
        }

        private static TextBox CreateSmallTextBox(Binding binding)
        {
            var tb = new TextBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                Style = TextBoxSmallStyle
            };
            tb.SetBinding(TextBox.TextProperty, binding);
            return tb;
        }

        private static Button CreateIconSpinButton(ICommand command)
        {
            var btn = new Button
            {
                Width = 25,
                Height = 25,
                Margin = new Thickness(5, 1, 5, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(2),
                Command = command
            };

            var glyph = new TextBlock
            {
                Text = "\uE713",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                Foreground = GlobalTextBrush
            };

            var rotate = new RotateTransform();
            glyph.RenderTransform = rotate;
            btn.Content = glyph;

            var anim = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                FillBehavior = FillBehavior.Stop
            };

            var storyboard = new Storyboard();
            storyboard.Children.Add(anim);
            Storyboard.SetTarget(anim, rotate);
            Storyboard.SetTargetProperty(anim, new PropertyPath(RotateTransform.AngleProperty));

            btn.Click += (_, __) => storyboard.Begin();
            return btn;
        }

        private static bool IsTextEditableType(Type t)
        {
            // Includes common primitives and nullable counterparts that can be edited via TextBox
            t = Nullable.GetUnderlyingType(t) ?? t;
            return t == typeof(int) ||
                   t == typeof(float) ||
                   t == typeof(uint) ||
                   t == typeof(long) ||
                   t == typeof(ulong) ||
                   t == typeof(sbyte) ||
                   t == typeof(double) ||
                   t == typeof(string);
        }
    }
}