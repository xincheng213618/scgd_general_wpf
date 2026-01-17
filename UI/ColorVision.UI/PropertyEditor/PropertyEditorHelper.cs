using ColorVision.Themes;
using ColorVision.UI.Extension;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
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
        public static ConcurrentDictionary<Type, IPropertyEditor> CustomEditorCache { get; } = new();
        private static readonly Dictionary<Type, Type> EditorTypeRegistry = new();
        private static readonly List<(Func<Type, bool> Predicate, Type EditorType)> TypePredicateRegistry = new();

        static PropertyEditorHelper()
        {
            var editorTypes = AssemblyHandler.Instance.GetAssemblies().SelectMany(a => a.GetTypes()) .Where(t => typeof(IPropertyEditor).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass);
            foreach (var type in editorTypes)
            {
                // 触发静态构造函数
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }
        }

        public static void RegisterEditor<TEditor>(Type targetType) where TEditor : IPropertyEditor, new()
        {
            EditorTypeRegistry[targetType] = typeof(TEditor);
        }
        public static void RegisterEditor<TEditor>(Func<Type, bool> typePredicate) where TEditor : IPropertyEditor, new()
        {
            TypePredicateRegistry.Add((typePredicate, typeof(TEditor)));
        }

        public static IPropertyEditor GetOrCreateEditor(Type editorType)
        {
            if (CustomEditorCache.TryGetValue(editorType, out var cachedEditor))
            {
                return cachedEditor;
            }

            if (Activator.CreateInstance(editorType) is IPropertyEditor newEditor)
            {
                CustomEditorCache[editorType] = newEditor;
                return newEditor;
            }

            throw new InvalidOperationException($"Could not create editor of type {editorType.Name}");
        }
        public static Type? GetEditorTypeForPropertyType(Type propertyType)
        {
            // Direct type match
            if (EditorTypeRegistry.TryGetValue(propertyType, out var editorType))
                return editorType;

            // Predicate match (first matching predicate wins)
            foreach (var (predicate, predicateEditorType) in TypePredicateRegistry)
            {
                if (predicate(propertyType))
                    return predicateEditorType;
            }

            return null;
        }

        public static List<Type> GetAllEditorTypesForPropertyType(Type propertyType)
        {
            var editorTypes = new List<Type>();

            // Direct type match
            if (EditorTypeRegistry.TryGetValue(propertyType, out var editorType))
                editorTypes.Add(editorType);

            // Predicate matches (all matching predicates)
            foreach (var (predicate, predicateEditorType) in TypePredicateRegistry)
            {
                if (predicate(propertyType))
                    editorTypes.Add(predicateEditorType);
            }

            return editorTypes;
        }

        public static T GetOrCreateEditor<T>() where T : IPropertyEditor, new()
        {
            var type = typeof(T);
            if (CustomEditorCache.TryGetValue(type, out var cachedEditor))
            {
                return (T)cachedEditor;
            }

            var newEditor = new T();
            CustomEditorCache[type] = newEditor;
            return newEditor;
        }

        private static readonly Lazy<ResourceCache> Resources = new(() => new ResourceCache());
        private class ResourceCache
        {
            public Brush GlobalTextBrush { get; set; }
            public Brush GlobalBorderBrush { get; set; }
            public Brush BorderBrush { get; set; }
            public Style ButtonCommandStyle { get; set; }
            public Style ComboBoxSmallStyle { get; set; }
            public Style TextBoxSmallStyle { get; set; }
            public IValueConverter Bool2VisibilityConverter { get; set; }
            public IValueConverter Bool2VisibilityReConverter { get; set; }
            public IValueConverter Enum2VisibilityConverter { get; set; }
            public IValueConverter Enum2VisibilityReConverter { get; set; }

            public void SetResources()
            {
                var app = Application.Current ?? throw new InvalidOperationException(Properties.Resources.ApplicationCurrentNotInitialized);

                GlobalTextBrush = (Brush)app.FindResource("GlobalTextBrush");
                GlobalBorderBrush = (Brush)app.FindResource("GlobalBorderBrush");
                BorderBrush = (Brush)app.FindResource("BorderBrush");
                ButtonCommandStyle = (Style)app.FindResource("ButtonCommand");
                ComboBoxSmallStyle = (Style)app.FindResource("ComboBox.Small");
                TextBoxSmallStyle = (Style)app.FindResource("TextBox.Small");
                
                // Required converter
                Bool2VisibilityConverter = app.TryFindResource("bool2VisibilityConverter") as IValueConverter
                    ?? throw new InvalidOperationException(Properties.Resources.Bool2VisibilityConverterNotFound);
                
                // Optional converters (may not be present in all themes)
                Bool2VisibilityReConverter = app.TryFindResource("bool2VisibilityConverter1") as IValueConverter;
                Enum2VisibilityConverter = app.TryFindResource("enum2VisibilityConverter") as IValueConverter;
                Enum2VisibilityReConverter = app.TryFindResource("enum2VisibilityConverter1") as IValueConverter;
            }

            public ResourceCache()
            {
                SetResources();
                ThemeManager.Current.CurrentUIThemeChanged += (e) => SetResources();
            }
        }

        public static Brush GlobalTextBrush => Resources.Value.GlobalTextBrush;
        public static Brush GlobalBorderBrush => Resources.Value.GlobalBorderBrush;
        public static Brush BorderBrush => Resources.Value.BorderBrush;
        public static Style ButtonCommandStyle => Resources.Value.ButtonCommandStyle;
        public static Style ComboBoxSmallStyle => Resources.Value.ComboBoxSmallStyle;
        public static Style TextBoxSmallStyle => Resources.Value.TextBoxSmallStyle;
        public static IValueConverter Bool2VisibilityConverter => Resources.Value.Bool2VisibilityConverter;
        public static IValueConverter Bool2VisibilityReConverter => Resources.Value.Bool2VisibilityReConverter;
        public static IValueConverter Enum2VisibilityConverter => Resources.Value.Enum2VisibilityConverter;
        public static IValueConverter Enum2VisibilityReConverter => Resources.Value.Enum2VisibilityReConverter;


        public static ResourceManager? GetResourceManager(object obj, ResourceManager resourceManager =null)
        {
            var type = obj.GetType();
            if (resourceManager == null)
            {
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
            else
            {
                ResourceManagerCache.AddOrUpdate(type, new Lazy<ResourceManager?>(() => resourceManager), (_, __) => new Lazy<ResourceManager?>(() => resourceManager));
                return resourceManager;
            }
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
                    Margin = new Thickness(5),
                    Padding = new Thickness(10, 8, 10, 8),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Background = (Brush)Application.Current.FindResource("GlobalBackground"),
                    BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
                    BorderThickness = new Thickness(1),
                    Command = command,
                    ToolTip = displayName,
                    Height = 70,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };

                var stackPanel = new StackPanel();

                var nameText = new TextBlock
                {
                    Text = displayName,
                    FontSize = 12,
                    FontWeight = FontWeights.Medium,
                    Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush"),
                    TextWrapping = TextWrapping.Wrap
                };
                stackPanel.Children.Add(nameText);

                var descriptionAttr = item.Prop.GetCustomAttribute<DescriptionAttribute>();
                var description = descriptionAttr?.Description ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(description))
                {
                    var assemblyText = new TextBlock
                    {
                        Text = description,
                        FontSize = 10,
                        Foreground = (Brush)Application.Current.FindResource("SecondaryTextBrush"),
                        Margin = new Thickness(0, 3, 0, 0),
                        Opacity = 0.7
                    };
                    stackPanel.Children.Add(assemblyText);
                }
                button.Content = stackPanel;

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
        static int GetInheritanceDepth(Type t)
        {
            int depth = 0;
            while (t != null)
            {
                t = t.BaseType;
                depth++;
            }
            return depth;
        }
        public static StackPanel GenPropertyEditorControl(object obj,ResourceManager resourceManager =null)
        {
            if (obj == null) return new StackPanel();

            if (resourceManager != null) GetResourceManager(obj, resourceManager);

            var categoryGroups = new Dictionary<string, List<PropertyInfo>>(StringComparer.Ordinal);

            void CollectProperties(object source)
            {
                var t = source.GetType();

                // 1. 获取属性
                var allProps = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                .Where(p => p.CanRead && p.CanWrite);

                // 2. 【关键修改】进行排序
                // GetInheritanceDepth 越小，说明越靠近基类 (object -> BaseConfig -> DeviceServiceConfig -> ConfigPG)
                // 如果您想要“原始信息”（基类）在最前面，请使用 OrderBy
                // 如果您想要“最上层”（派生类）在最前面，请使用 OrderByDescending
                var sortedProps = allProps.OrderBy(p => GetInheritanceDepth(p.DeclaringType));

                // 如果希望同一类中的属性按元数据Token（近似代码声明顺序）排序，可以再接一个 ThenBy
                //var sortedProps = allProps.OrderBy(p => GetInheritanceDepth(p.DeclaringType))
                //                          .ThenBy(p => p.MetadataToken);

                foreach (var prop in sortedProps)
                {
                    var browsableAttr = prop.GetCustomAttribute<BrowsableAttribute>();
                    if (!(browsableAttr?.Browsable ?? true))
                        continue;

                    var categoryAttr = prop.GetCustomAttribute<CategoryAttribute>();
                    string category = categoryAttr?.Category ?? t.Name;

                    if (!categoryGroups.TryGetValue(category, out var list))
                    {
                        list = new List<PropertyInfo>();
                        categoryGroups[category] = list;
                    }
                    list.Add(prop);
                }
            }

            var propertyPanel = new StackPanel();
            CollectProperties(obj);
            
            foreach (var categoryGroup in categoryGroups)
            {
                var border = new Border
                {
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(0, 0, 0, 5)
                };
                border.SetResourceReference(Border.BackgroundProperty, "GlobalBorderBrush");
                border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

                var stackPanel = new StackPanel { Margin = new Thickness(5, 5, 5, 0) };


                var categoryHeader = new TextBlock
                {
                    Text = categoryGroup.Key,
                    FontWeight = FontWeights.Bold,
                    Foreground = GlobalTextBrush,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                categoryHeader.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                stackPanel.Children.Add(categoryHeader);


                border.Child = stackPanel;
                propertyPanel.Children.Add(border);

                foreach (var property in categoryGroup.Value)
                {
                    DockPanel dockPanel = null;
                    var editorAttr = property.GetCustomAttribute<PropertyEditorTypeAttribute>();
                    if (editorAttr?.EditorType != null)
                    {
                        try
                        {
                            var editor = GetOrCreateEditor(editorAttr.EditorType);
                            dockPanel = editor.GenProperties(property, obj);
                        }
                        catch (Exception)
                        {
                        }
                    }

                    if (dockPanel == null)
                    {
                        Type? editorType = null;
                        editorType = GetEditorTypeForPropertyType(property.PropertyType);
                        if (editorType != null)
                        {
                            try
                            {
                                var editor = GetOrCreateEditor(editorType);
                                dockPanel = editor.GenProperties(property, obj);
                            }
                            catch (Exception)
                            {
                                continue; 
                            }
                        }
                        else if (typeof(INotifyPropertyChanged).IsAssignableFrom(property.PropertyType))
                        {
                            // 如果属性是ViewModelBase的子类，递归解析
                            var nestedObj = (INotifyPropertyChanged)property.GetValue(obj);
                            if (nestedObj != null)
                            {
                                stackPanel.Margin = new Thickness(5);
                                StackPanel stackPanel1 = PropertyEditorHelper.GenPropertyEditorControl(nestedObj);
                                if (stackPanel1.Children.Count == 1 && stackPanel1.Children[0] is Border border1 && border1.Child is StackPanel stackPanel2 && stackPanel2.Children.Count > 1)
                                {
                                    stackPanel.Children.Add(stackPanel1);
                                }
                                continue;
                            }
                        }
                        else if (property.PropertyType == typeof(object))
                        {
                            stackPanel.Margin = new Thickness(5);
                            StackPanel stackPanel1 = PropertyEditorHelper.GenPropertyEditorControl(property.GetValue(obj));
                            if (stackPanel1.Children.Count == 1 && stackPanel1.Children[0] is Border border1 && border1.Child is StackPanel stackPanel2 && stackPanel2.Children.Count != 0)
                            {
                                stackPanel.Children.Add(stackPanel1);
                            }
                            continue;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    dockPanel.Margin = new Thickness(0, 0, 0, 5);

                    // Visibility binding based on PropertyVisibilityAttribute
                    var visibleAttr = property.GetCustomAttribute<PropertyVisibilityAttribute>();
                    if (visibleAttr != null)
                    {
                        var vb = new Binding(visibleAttr.PropertyName)
                        {
                            Source = obj,
                            Mode = BindingMode.OneWay
                        };

                        IValueConverter? converter = null;
                        
                        // If ExpectedValue is set, this is an enum binding
                        if (visibleAttr.ExpectedValue != null)
                        {
                            converter = visibleAttr.IsInverted ? Enum2VisibilityReConverter : Enum2VisibilityConverter;
                            vb.ConverterParameter = visibleAttr.ExpectedValue;
                            
                            if (converter == null)
                            {
                                // Enum converters not available - skip binding
                                // This can happen if the theme doesn't include them
                                continue;
                            }
                        }
                        else
                        {
                            // Boolean binding - support both normal and inverted
                            converter = visibleAttr.IsInverted ? Bool2VisibilityReConverter : Bool2VisibilityConverter;
                            
                            // If the required converter is not available, we cannot bind correctly
                            // The standard converter is always available, so only the inverted version might be missing
                            if (converter == null)
                            {
                                // Cannot use IsInverted without the reversed converter - skip binding
                                // to avoid incorrect visibility behavior
                                continue;
                            }
                        }

                        vb.Converter = converter;
                        dockPanel.SetBinding(UIElement.VisibilityProperty, vb);
                    }

                    stackPanel.Children.Add(dockPanel);
                }
            }

            return propertyPanel;
        }

        // Helpers

        public static string GetDisplayName(ResourceManager? rm, PropertyInfo prop, string? overrideName = null)
        {
            var displayNameAttr = prop.GetCustomAttribute<DisplayNameAttribute>();
            var raw = overrideName ?? displayNameAttr?.DisplayName ?? prop.Name;
            return rm?.GetString(raw, Thread.CurrentThread.CurrentUICulture) ?? raw;
        }

        public static TextBlock CreateLabel(PropertyInfo property, ResourceManager? rm)
        {
            var desc = property.GetCustomAttribute<DescriptionAttribute>()?.Description;
            var tb = new TextBlock
            {
                Text = GetDisplayName(rm, property),
                MinWidth = LabelMinWidth,
                ToolTip = string.IsNullOrWhiteSpace(desc) ? null : desc
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");

            return tb;
        }

        public static Binding CreateTwoWayBinding(object source, string path)
        {
            return new Binding(path)
            {
                Source = source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
        }

        public static TextBox CreateSmallTextBox(Binding binding)
        {
            var tb = new TextBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                Style = TextBoxSmallStyle
            };
            tb.SetBinding(TextBox.TextProperty, binding);
            return tb;
        }

        public static Button CreateIconSpinButton(ICommand command)
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
            };
            glyph.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");

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

        /// <summary>
        /// Converts a value to the specified target type, handling string-to-numeric conversions.
        /// </summary>
        /// <param name="value">The value to convert</param>
        /// <param name="targetType">The target type to convert to</param>
        /// <returns>The converted value, or a default value if conversion fails</returns>
        public static object? ConvertToTargetType(object? value, Type targetType)
        {
            if (value == null)
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            var valueType = value.GetType();
            
            // If the value is already the correct type, return it directly
            if (valueType == targetType || targetType.IsAssignableFrom(valueType))
            {
                return value;
            }

            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // Convert from string for numeric types
            if (value is string strValue)
            {
                if (string.IsNullOrWhiteSpace(strValue))
                {
                    return underlyingType.IsValueType ? Activator.CreateInstance(underlyingType) : null;
                }

                try
                {
                    if (underlyingType == typeof(int))
                        return int.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(long))
                        return long.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(short))
                        return short.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(byte))
                        return byte.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(uint))
                        return uint.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(ulong))
                        return ulong.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(ushort))
                        return ushort.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(sbyte))
                        return sbyte.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(float))
                        return float.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(double))
                        return double.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(decimal))
                        return decimal.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(bool))
                        return bool.Parse(strValue);

                    return Convert.ChangeType(strValue, underlyingType, System.Globalization.CultureInfo.InvariantCulture);
                }
                catch
                {
                    return underlyingType.IsValueType ? Activator.CreateInstance(underlyingType) : null;
                }
            }

            // Try direct conversion for other types
            try
            {
                return Convert.ChangeType(value, underlyingType, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return value;
            }
        }
    }
}