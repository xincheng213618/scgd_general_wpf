#pragma warning disable CA1707,CA1852,CS8601
using ColorVision.Themes;
using ColorVision.UI.Extension;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
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
        private static readonly ConcurrentDictionary<(ResourceManager ResourceManager, string CultureName, string Key), string> ResourceStringCache = new();
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

        internal static bool HasEditorForProperty(PropertyInfo property)
        {
            var editorAttr = property.GetCustomAttribute<PropertyEditorTypeAttribute>();
            return editorAttr?.EditorType != null || GetEditorTypeForPropertyType(property.PropertyType) != null;
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


        public static ResourceManager? GetResourceManager(object obj, ResourceManager? resourceManager = null)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return GetResourceManager(obj.GetType(), resourceManager);
        }

        public static ResourceManager? GetResourceManager(Type type, ResourceManager? resourceManager = null)
        {
            ArgumentNullException.ThrowIfNull(type);
            if (resourceManager != null)
            {
                ResourceManagerCache.AddOrUpdate(type, new Lazy<ResourceManager?>(() => resourceManager), (_, __) => new Lazy<ResourceManager?>(() => resourceManager));
                return resourceManager;
            }

            var lazyResourceManager = ResourceManagerCache.GetOrAdd(type, t => new Lazy<ResourceManager?>(() =>
            {
                try
                {
                    string namespaceName = t.Assembly.GetName().Name!;
                    string resourceClassName = $"{namespaceName}.Properties.Resources";
                    Type? resourceType = t.Assembly.GetType(resourceClassName);
                    if (resourceType != null)
                    {
                        var rmProp = resourceType.GetProperty(nameof(ResourceManager), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
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
                var description = GetLocalizedString(rm, descriptionAttr?.Description);
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

        public static DockPanel GenProperties(PropertyInfo property, object obj)
        {
            ArgumentNullException.ThrowIfNull(property);
            ArgumentNullException.ThrowIfNull(obj);

            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance) { obj };
            if (TryCreatePropertyDockPanel(property, obj, visited, out var dockPanel))
            {
                return dockPanel;
            }

            throw new NotSupportedException($"No property editor registered for property '{property.Name}' of type '{property.PropertyType.FullName}'.");
        }

        internal static bool TryCreatePropertyDockPanel(PropertyInfo property, object obj, out DockPanel dockPanel)
        {
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance) { obj };
            return TryCreatePropertyDockPanel(property, obj, visited, out dockPanel);
        }

        private static bool TryCreatePropertyDockPanel(PropertyInfo property, object obj, HashSet<object> visited, out DockPanel dockPanel)
        {
            dockPanel = null!;
            if (property == null || obj == null || property.GetIndexParameters().Length != 0)
            {
                return false;
            }

            try
            {
                DockPanel? createdPanel = null;
                var editorAttr = property.GetCustomAttribute<PropertyEditorTypeAttribute>();
                if (editorAttr?.EditorType != null)
                {
                    try
                    {
                        var editor = GetOrCreateEditor(editorAttr.EditorType);
                        createdPanel = editor.GenProperties(property, obj);
                    }
                    catch (Exception)
                    {
                    }
                }

                if (createdPanel == null)
                {
                    var editorType = GetEditorTypeForPropertyType(property.PropertyType);
                    if (editorType != null)
                    {
                        try
                        {
                            var editor = GetOrCreateEditor(editorType);
                            createdPanel = editor.GenProperties(property, obj);
                        }
                        catch (Exception)
                        {
                        }
                    }
                    else
                    {
                        TryCreateNestedDockPanel(property, obj, visited, out createdPanel);
                    }
                }

                if (createdPanel == null)
                {
                    return false;
                }

                createdPanel.Margin = new Thickness(0, 0, 0, 5);
                createdPanel.Tag = property;
                ApplyVisibilityBinding(createdPanel, property, obj);
                dockPanel = createdPanel;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryCreateNestedPropertyPanel(PropertyInfo property, object obj, out StackPanel nestedPanel)
        {
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance) { obj };
            return TryCreateNestedPropertyPanel(property, obj, visited, out nestedPanel);
        }

        private static bool TryCreateNestedDockPanel(PropertyInfo property, object obj, HashSet<object> visited, out DockPanel? dockPanel)
        {
            dockPanel = null;
            if (!TryCreateNestedPropertyPanel(property, obj, visited, out var nestedPanel))
            {
                return false;
            }

            var rm = GetResourceManager(obj);
            var label = CreateLabel(property, rm);
            label.FontWeight = FontWeights.SemiBold;
            label.Margin = new Thickness(0, 0, 0, 5);

            nestedPanel.Margin = new Thickness(10, 0, 0, 0);

            dockPanel = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(label, Dock.Top);
            dockPanel.Children.Add(label);
            dockPanel.Children.Add(nestedPanel);
            return true;
        }

        private static bool TryCreateNestedPropertyPanel(PropertyInfo property, object obj, HashSet<object> visited, out StackPanel nestedPanel)
        {
            nestedPanel = new StackPanel();
            if (!TryGetNestedPropertyValue(property, obj, visited, out var nestedValue))
            {
                return false;
            }

            nestedPanel = GenPropertyEditorControl(nestedValue, null, visited);
            return HasEditorContent(nestedPanel);
        }

        private static bool TryGetNestedPropertyValue(PropertyInfo property, object obj, HashSet<object> visited, out object nestedValue)
        {
            nestedValue = null!;
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
            {
                return false;
            }

            try
            {
                nestedValue = property.GetValue(obj)!;
            }
            catch
            {
                return false;
            }

            if (nestedValue == null || visited.Contains(nestedValue))
            {
                return false;
            }

            return CanGenerateNestedEditor(nestedValue.GetType());
        }

        private static bool CanGenerateNestedEditor(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (!type.IsClass || type == typeof(string))
            {
                return false;
            }

            if (typeof(Delegate).IsAssignableFrom(type) || typeof(Type).IsAssignableFrom(type) || typeof(ResourceManager).IsAssignableFrom(type))
            {
                return false;
            }

            if (typeof(DependencyObject).IsAssignableFrom(type) || typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
            {
                return false;
            }

            if (IsFrameworkType(type) && !typeof(INotifyPropertyChanged).IsAssignableFrom(type))
            {
                return false;
            }

            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0 && (p.GetCustomAttribute<BrowsableAttribute>()?.Browsable ?? true));
        }

        private static bool IsFrameworkType(Type type)
        {
            var namespaceName = type.Namespace ?? string.Empty;
            return namespaceName == "System"
                || namespaceName.StartsWith("System.", StringComparison.Ordinal)
                || namespaceName.StartsWith("Microsoft.", StringComparison.Ordinal)
                || namespaceName.StartsWith("MS.", StringComparison.Ordinal);
        }

        private static bool HasEditorContent(StackPanel panel)
        {
            return panel.Children.OfType<Border>()
                .Any(border => border.Child is StackPanel stackPanel && stackPanel.Children.Count > 1);
        }

        public static DockPanel GenProperties(object obj, string propertyName, ResourceManager? resourceManager = null)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException("Property name cannot be empty.", nameof(propertyName));
            }

            if (resourceManager != null)
            {
                GetResourceManager(obj, resourceManager);
            }

            var property = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new ArgumentException($"Property '{propertyName}' was not found on type '{obj.GetType().Name}'.", nameof(propertyName));

            if (!property.CanRead || !property.CanWrite)
            {
                throw new ArgumentException($"Property '{propertyName}' must be a public readable and writable instance property.", nameof(propertyName));
            }

            return GenProperties(property, obj);
        }

        public static DockPanel GenProperties<T>(T obj, System.Linq.Expressions.Expression<Func<T, object?>> propertyExpression, ResourceManager? resourceManager = null)
        {
            ArgumentNullException.ThrowIfNull(obj);
            ArgumentNullException.ThrowIfNull(propertyExpression);

            if (resourceManager != null)
            {
                GetResourceManager(obj, resourceManager);
            }

            return GenProperties(GetPropertyInfo(propertyExpression), obj);
        }

        private static PropertyInfo GetPropertyInfo<T>(System.Linq.Expressions.Expression<Func<T, object?>> propertyExpression)
        {
            System.Linq.Expressions.Expression body = propertyExpression.Body;
            if (body is System.Linq.Expressions.UnaryExpression unaryExpression &&
                (unaryExpression.NodeType == System.Linq.Expressions.ExpressionType.Convert || unaryExpression.NodeType == System.Linq.Expressions.ExpressionType.ConvertChecked))
            {
                body = unaryExpression.Operand;
            }

            if (body is System.Linq.Expressions.MemberExpression memberExpression && memberExpression.Member is PropertyInfo propertyInfo)
            {
                return propertyInfo;
            }

            throw new ArgumentException("Expression must select a property, for example: x => x.Mode.", nameof(propertyExpression));
        }

        private static void ApplyVisibilityBinding(DockPanel dockPanel, PropertyInfo property, object obj)
        {
            var visibleAttr = property.GetCustomAttribute<PropertyVisibilityAttribute>();
            if (visibleAttr == null)
            {
                return;
            }

            var binding = new Binding(visibleAttr.PropertyName)
            {
                Source = obj,
                Mode = BindingMode.OneWay
            };

            IValueConverter? converter;
            if (visibleAttr.ExpectedValue != null)
            {
                converter = visibleAttr.IsInverted ? Enum2VisibilityReConverter : Enum2VisibilityConverter;
                binding.ConverterParameter = visibleAttr.ExpectedValue;
            }
            else
            {
                converter = visibleAttr.IsInverted ? Bool2VisibilityReConverter : Bool2VisibilityConverter;
            }

            if (converter == null)
            {
                return;
            }

            binding.Converter = converter;
            dockPanel.SetBinding(UIElement.VisibilityProperty, binding);
        }

        public static StackPanel GenPropertyEditorControl(object obj, ResourceManager? resourceManager = null)
        {
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            return GenPropertyEditorControl(obj, resourceManager, visited);
        }

        private static StackPanel GenPropertyEditorControl(object obj, ResourceManager? resourceManager, HashSet<object> visited)
        {
            if (obj == null) return new StackPanel();
            if (!visited.Add(obj)) return new StackPanel();

            try
            {
                bool orderBy = true;
                if (resourceManager != null)
                {
                    orderBy = false;
                    GetResourceManager(obj, resourceManager);
                }

                var categoryGroups = new Dictionary<string, List<PropertyInfo>>(StringComparer.Ordinal);

                void CollectProperties(object source)
                {
                    var type = source.GetType();

                    // 1. 获取属性
                    var allProps = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                    .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);


                    var sortedProps = orderBy ? allProps.OrderBy(p => GetInheritanceDepth(p.DeclaringType ?? type)) : allProps.OrderByDescending(p => GetInheritanceDepth(p.DeclaringType ?? type));

                    foreach (var prop in sortedProps)
                    {
                        var browsableAttr = prop.GetCustomAttribute<BrowsableAttribute>();
                        if (!(browsableAttr?.Browsable ?? true))
                            continue;

                        var categoryAttr = prop.GetCustomAttribute<CategoryAttribute>();
                        string category = categoryAttr?.Category ?? type.Name;

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
                        Margin = new Thickness(0, 0, 0, 5),
                        Tag = categoryGroup.Key
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

                    foreach (var property in categoryGroup.Value)
                    {
                        if (TryCreatePropertyDockPanel(property, obj, visited, out var dockPanel))
                        {
                            stackPanel.Children.Add(dockPanel);
                        }
                    }

                    if (stackPanel.Children.Count > 1)
                    {
                        propertyPanel.Children.Add(border);
                    }
                }

                return propertyPanel;
            }

            finally
            {
                visited.Remove(obj);
            }
        }


        // Helpers

        public static string GetDisplayName(ResourceManager? rm, PropertyInfo prop, string? overrideName = null)
        {
            var displayNameAttr = prop.GetCustomAttribute<DisplayNameAttribute>();
            var raw = overrideName ?? displayNameAttr?.DisplayName ?? prop.Name;
            return GetLocalizedString(rm, raw);
        }

        public static string GetLocalizedString(ResourceManager? rm, string? key)
        {
            if (rm == null || string.IsNullOrWhiteSpace(key))
            {
                return key ?? string.Empty;
            }

            var culture = CultureInfo.CurrentUICulture;
            return ResourceStringCache.GetOrAdd((rm, culture.Name, key), _ =>
            {
                try
                {
                    return rm.GetString(key, culture) ?? key;
                }
                catch
                {
                    return key;
                }
            });
        }

        public static TextBlock CreateLabel(PropertyInfo property, ResourceManager? rm)
        {
            var desc = GetLocalizedString(rm, property.GetCustomAttribute<DescriptionAttribute>()?.Description);
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
