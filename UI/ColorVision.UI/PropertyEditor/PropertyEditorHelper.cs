#pragma warning disable CA1707,CA1852,CS8601
using ColorVision.Themes;
using ColorVision.UI.Extension;
using log4net;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ColorVision.UI
{
    public interface IPropertyEditorMetadataProvider
    {
        bool IsPropertyManaged(PropertyInfo propertyInfo);
        bool IsBrowsable(PropertyInfo propertyInfo);
        Type? GetEditorType(PropertyInfo propertyInfo);
        string? GetDisplayName(PropertyInfo propertyInfo);
        string? GetDescription(PropertyInfo propertyInfo);
        string? GetCategory(PropertyInfo propertyInfo);
    }

    public static partial class PropertyEditorHelper
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PropertyEditorHelper));
        private static readonly PropertyEditorRegistry EditorRegistry = new(Log);

        // Constants
        public const double LabelMinWidth = 120;
        public const double ControlMinWidth = 150;

        // Cache for resources and reflection results
        public static ConcurrentDictionary<Type, Lazy<ResourceManager?>> ResourceManagerCache { get; set; } = new();
        private static readonly ConcurrentDictionary<(ResourceManager ResourceManager, string CultureName, string Key), string> ResourceStringCache = new();
        public static ConcurrentDictionary<Type, IPropertyEditor> CustomEditorCache => EditorRegistry.Instances;
        private static readonly AsyncLocal<IPropertyEditorMetadataProvider?> MetadataProviderContext = new();

        static PropertyEditorHelper()
        {
            PropertyEditorBuiltIns.Register(EditorRegistry);
            try
            {
                var editorTypes = AssemblyHandler.Instance.GetAssemblies()
                    .Where(assembly => assembly != typeof(PropertyEditorHelper).Assembly)
                    .SelectMany(assembly => AssemblyHandler.Instance.GetTypes(assembly))
                    .Where(type => typeof(IPropertyEditor).IsAssignableFrom(type) && !type.IsAbstract && type.IsClass);
                foreach (Type type in editorTypes)
                {
                    try
                    {
                        RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Failed to initialize property editor '{type.FullName}'.", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to initialize property editors.", ex);
            }
        }

        public static void RegisterEditor<TEditor>(Type targetType) where TEditor : IPropertyEditor, new()
        {
            EditorRegistry.Register<TEditor>(targetType);
        }

        public static void RegisterEditor<TEditor>(Func<Type, bool> typePredicate) where TEditor : IPropertyEditor, new()
        {
            EditorRegistry.Register<TEditor>(typePredicate);
        }

        public static IPropertyEditor GetOrCreateEditor(Type editorType)
        {
            return EditorRegistry.GetOrCreate(editorType);
        }
        public static Type? GetEditorTypeForPropertyType(Type propertyType)
        {
            return EditorRegistry.Find(propertyType);
        }

        public static List<Type> GetAllEditorTypesForPropertyType(Type propertyType)
        {
            return EditorRegistry.FindAll(propertyType);
        }

        internal static bool HasEditorForProperty(PropertyInfo property)
        {
            var editorAttr = property.GetCustomAttribute<PropertyEditorTypeAttribute>();
            return editorAttr?.EditorType != null || GetEditorTypeForPropertyType(property.PropertyType) != null;
        }

        public static PropertyInfo[] GetEditableProperties(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.CanRead && property.CanWrite)
                .Where(property => property.GetIndexParameters().Length == 0)
                .Where(property => property.GetCustomAttribute<BrowsableAttribute>()?.Browsable ?? true)
                .ToArray();
        }

        public static bool CanEditProperty(PropertyInfo property)
        {
            ArgumentNullException.ThrowIfNull(property);
            return HasEditorForProperty(property) || CanGenerateNestedEditor(property.PropertyType);
        }

        public static T GetOrCreateEditor<T>() where T : IPropertyEditor, new()
        {
            return (T)EditorRegistry.GetOrCreate(typeof(T));
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
            var comboBox = new ComboBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = ControlMinWidth,
                Style = ComboBoxSmallStyle,
                ItemsSource = Enum.GetValues(property.PropertyType)
            };

            var binding = CreateTwoWayBinding(obj, property);
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

        private static int GetDisplayOrder(PropertyInfo property)
        {
            return property.GetCustomAttribute<DisplayAttribute>()?.GetOrder() ?? 0;
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
                var metadataEditorType = MetadataProviderContext.Value?.GetEditorType(property);
                if (metadataEditorType != null)
                    createdPanel = TryGenerateEditor(metadataEditorType, property, obj);

                var editorAttr = property.GetCustomAttribute<PropertyEditorTypeAttribute>();
                if (createdPanel == null && editorAttr?.EditorType != null)
                    createdPanel = TryGenerateEditor(editorAttr.EditorType, property, obj);

                if (createdPanel == null)
                {
                    var editorType = GetEditorTypeForPropertyType(property.PropertyType);
                    if (editorType != null)
                        createdPanel = TryGenerateEditor(editorType, property, obj);
                    else
                        TryCreateNestedDockPanel(property, obj, visited, out createdPanel);
                }

                if (createdPanel == null)
                {
                    return false;
                }

                createdPanel.Margin = new Thickness(0, 0, 0, 5);
                createdPanel.Tag = property;
                if (property.GetCustomAttribute<ReadOnlyAttribute>()?.IsReadOnly == true)
                    createdPanel.IsEnabled = false;
                ApplyVisibilityBinding(createdPanel, property, obj);
                dockPanel = createdPanel;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to create property editor for '{obj.GetType().FullName}.{property.Name}'.", ex);
                return false;
            }
        }

        private static DockPanel? TryGenerateEditor(Type editorType, PropertyInfo property, object obj)
        {
            try
            {
                return GetOrCreateEditor(editorType).GenProperties(property, obj);
            }
            catch (Exception ex)
            {
                Log.Error($"Property editor '{editorType.FullName}' failed for '{obj.GetType().FullName}.{property.Name}'.", ex);
                return null;
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

        public static void ApplyVisibilityBinding(FrameworkElement element, PropertyInfo property, object obj)
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
            element.SetBinding(UIElement.VisibilityProperty, binding);
        }

        public static StackPanel GenPropertyEditorControl(
            object obj,
            ResourceManager? resourceManager = null,
            bool showCategoryHeader = true,
            IPropertyEditorMetadataProvider? metadataProvider = null,
            PropertyEditorAdvancedOptions? advancedOptions = null)
        {
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            if (advancedOptions == null)
                return GenerateWithMetadataProvider(obj, resourceManager, visited, showCategoryHeader, metadataProvider);

            var propertyPanel = new StackPanel();
            void Rebuild()
            {
                var generatedPanel = GenerateWithMetadataProvider(obj, resourceManager, visited, showCategoryHeader, metadataProvider, advancedOptions, Rebuild);
                propertyPanel.Children.Clear();
                while (generatedPanel.Children.Count > 0)
                {
                    UIElement child = generatedPanel.Children[0];
                    generatedPanel.Children.RemoveAt(0);
                    propertyPanel.Children.Add(child);
                }
            }

            Rebuild();
            return propertyPanel;
        }

        private static StackPanel GenerateWithMetadataProvider(
            object obj,
            ResourceManager? resourceManager,
            HashSet<object> visited,
            bool showCategoryHeader,
            IPropertyEditorMetadataProvider? metadataProvider,
            PropertyEditorAdvancedOptions? advancedOptions = null,
            Action? advancedChanged = null)
        {
            var previousProvider = MetadataProviderContext.Value;
            if (metadataProvider != null)
                MetadataProviderContext.Value = metadataProvider;

            try
            {
                return GenPropertyEditorControl(obj, resourceManager, visited, showCategoryHeader, advancedOptions, advancedChanged);
            }
            finally
            {
                if (metadataProvider != null)
                    MetadataProviderContext.Value = previousProvider;
            }
        }

        private static StackPanel GenPropertyEditorControl(
            object obj,
            ResourceManager? resourceManager,
            HashSet<object> visited,
            bool showCategoryHeader = true,
            PropertyEditorAdvancedOptions? advancedOptions = null,
            Action? advancedChanged = null)
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
                    var metadataProvider = MetadataProviderContext.Value;

                    // 1. 获取属性
                    var allProps = GetEditableProperties(type).ToList();

                    if (metadataProvider != null)
                    {
                        bool hasManagedProperties = allProps.Any(metadataProvider.IsPropertyManaged);
                        allProps = allProps
                            .Where(p => (!hasManagedProperties || metadataProvider.IsPropertyManaged(p)) && metadataProvider.IsBrowsable(p))
                            .ToList();
                    }

                    var sortedProps = orderBy
                        ? allProps.OrderBy(GetDisplayOrder).ThenBy(p => GetInheritanceDepth(p.DeclaringType ?? type))
                        : allProps.OrderBy(GetDisplayOrder).ThenByDescending(p => GetInheritanceDepth(p.DeclaringType ?? type));

                    foreach (var prop in sortedProps)
                    {
                        var categoryAttr = prop.GetCustomAttribute<CategoryAttribute>();
                        string category = metadataProvider?.IsPropertyManaged(prop) == true
                            ? metadataProvider.GetCategory(prop) ?? categoryAttr?.Category ?? type.Name
                            : categoryAttr?.Category ?? type.Name;

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
                bool hasAdvancedProperties = advancedOptions != null && categoryGroups.Values.SelectMany(properties => properties).Any(property => advancedOptions.IsAdvancedProperty(property));
                bool hasStandardProperties = advancedOptions == null || categoryGroups.Values.SelectMany(properties => properties).Any(property => !advancedOptions.IsAdvancedProperty(property));
                bool advancedToggleAdded = false;
                bool categoryHeaderAdded = false;

                foreach (var categoryGroup in categoryGroups)
                {
                    var visibleProperties = advancedOptions?.ShowAdvancedProperties == false
                        ? categoryGroup.Value.Where(property => !advancedOptions.IsAdvancedProperty(property)).ToList()
                        : categoryGroup.Value;
                    bool addAdvancedToggle = showCategoryHeader
                        && advancedOptions?.ShowAdvancedToggleInCategoryHeader != false
                        && hasAdvancedProperties
                        && !advancedToggleAdded
                        && (visibleProperties.Count > 0 || !hasStandardProperties);
                    if (visibleProperties.Count == 0 && !addAdvancedToggle)
                        continue;

                    bool useIntegratedLayout = advancedOptions?.UseIntegratedCategoryLayout == true;
                    var stackPanel = new StackPanel
                    {
                        Margin = useIntegratedLayout
                            ? new Thickness(5, 4, 5, 6)
                            : showCategoryHeader ? new Thickness(5, 5, 5, 0) : new Thickness(5),
                        Tag = categoryGroup.Key
                    };


                    if (showCategoryHeader)
                    {
                        bool showCurrentCategoryHeader = categoryHeaderAdded || advancedOptions?.ShowFirstCategoryHeader != false;
                        if (showCurrentCategoryHeader)
                        {
                            var categoryHeader = CreateCategoryHeader(categoryGroup.Key, addAdvancedToggle ? advancedOptions : null, advancedChanged);
                            stackPanel.Children.Add(categoryHeader);
                        }
                        categoryHeaderAdded = true;
                        advancedToggleAdded |= addAdvancedToggle;
                    }


                    int propertyEditorCount = 0;
                    foreach (var property in visibleProperties)
                    {
                        if (TryCreatePropertyDockPanel(property, obj, visited, out var dockPanel))
                        {
                            stackPanel.Children.Add(dockPanel);
                            propertyEditorCount++;
                        }
                    }

                    if (propertyEditorCount > 0 || addAdvancedToggle)
                    {
                        if (useIntegratedLayout)
                        {
                            propertyPanel.Children.Add(stackPanel);
                        }
                        else
                        {
                            var border = new Border
                            {
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(5),
                                Margin = new Thickness(0, 0, 0, 5),
                                Tag = categoryGroup.Key,
                                Child = stackPanel
                            };
                            border.SetResourceReference(Border.BackgroundProperty, "GlobalBorderBrush");
                            border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
                            propertyPanel.Children.Add(border);
                        }
                    }
                }

                return propertyPanel;
            }

            finally
            {
                visited.Remove(obj);
            }
        }

        private static DockPanel CreateCategoryHeader(string title, PropertyEditorAdvancedOptions? advancedOptions, Action? advancedChanged)
        {
            var header = new DockPanel
            {
                LastChildFill = true,
                Margin = new Thickness(0, 0, 0, 5)
            };

            if (advancedOptions != null)
            {
                Canvas icon = CreateAdvancedFilterIcon(advancedOptions.ShowAdvancedProperties);

                var toggle = new ToggleButton
                {
                    Width = 24,
                    Height = 20,
                    Padding = new Thickness(0),
                    Margin = new Thickness(5, -2, 0, 0),
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent,
                    Content = icon,
                    IsChecked = advancedOptions.ShowAdvancedProperties,
                    ToolTip = advancedOptions.ToolTip,
                    Focusable = false
                };
                AutomationProperties.SetName(toggle, advancedOptions.ToolTip);
                toggle.Checked += (_, _) =>
                {
                    if (advancedOptions.ShowAdvancedProperties)
                        return;

                    advancedOptions.ShowAdvancedProperties = true;
                    advancedChanged?.Invoke();
                };
                toggle.Unchecked += (_, _) =>
                {
                    if (!advancedOptions.ShowAdvancedProperties)
                        return;

                    advancedOptions.ShowAdvancedProperties = false;
                    advancedChanged?.Invoke();
                };
                DockPanel.SetDock(toggle, Dock.Right);
                header.Children.Add(toggle);
            }

            var titleText = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                Foreground = GlobalTextBrush
            };
            titleText.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            header.Children.Add(titleText);
            return header;
        }

        private static Canvas CreateAdvancedFilterIcon(bool isActive)
        {
            const double iconSize = 16;
            string brushKey = isActive ? "PrimaryBrush" : "GlobalTextBrush";
            var icon = new Canvas
            {
                Width = iconSize,
                Height = iconSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true
            };

            AddSlider(3.5, 6.5);
            AddSlider(8, 11);
            AddSlider(12.5, 4.5);
            return icon;

            void AddSlider(double y, double knobX)
            {
                var line = new Line
                {
                    X1 = 1.5,
                    X2 = 14.5,
                    Y1 = y,
                    Y2 = y,
                    StrokeThickness = 1.25,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                line.SetResourceReference(Shape.StrokeProperty, brushKey);
                icon.Children.Add(line);

                var knob = new Ellipse
                {
                    Width = 3.5,
                    Height = 3.5
                };
                knob.SetResourceReference(Shape.FillProperty, brushKey);
                Canvas.SetLeft(knob, knobX - 1.75);
                Canvas.SetTop(knob, y - 1.75);
                icon.Children.Add(knob);
            }
        }


        // Helpers

        public static string GetDisplayName(ResourceManager? rm, PropertyInfo prop, string? overrideName = null)
        {
            var displayNameAttr = prop.GetCustomAttribute<DisplayNameAttribute>();
            var metadataProvider = MetadataProviderContext.Value;
            var metadataName = metadataProvider?.IsPropertyManaged(prop) == true
                ? metadataProvider.GetDisplayName(prop)
                : null;
            var raw = overrideName ?? metadataName ?? displayNameAttr?.DisplayName ?? prop.Name;
            return GetLocalizedString(rm, raw);
        }

        public static string GetDescription(ResourceManager? rm, PropertyInfo prop)
        {
            var metadataProvider = MetadataProviderContext.Value;
            var metadataDescription = metadataProvider?.IsPropertyManaged(prop) == true
                ? metadataProvider.GetDescription(prop)
                : null;
            var raw = metadataDescription ?? prop.GetCustomAttribute<DescriptionAttribute>()?.Description;

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
            var desc = GetDescription(rm, property);
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
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                ValidatesOnExceptions = true,
                ValidatesOnDataErrors = true,
                NotifyOnValidationError = true
            };
        }

        public static Binding CreateTwoWayBinding(object source, PropertyInfo property, UpdateSourceTrigger defaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged)
        {
            ArgumentNullException.ThrowIfNull(property);
            Binding binding = CreateTwoWayBinding(source, property.Name);
            if (!property.CanWrite || property.GetCustomAttribute<ReadOnlyAttribute>()?.IsReadOnly == true)
                binding.Mode = BindingMode.OneWay;
            binding.UpdateSourceTrigger = property.GetCustomAttribute<PropertyEditorTypeAttribute>()?.UpdateSourceTrigger
                ?? defaultUpdateSourceTrigger;
            return binding;
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
