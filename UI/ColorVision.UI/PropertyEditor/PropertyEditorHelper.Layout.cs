#pragma warning disable CA1707,CA1852,CS8601
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ColorVision.UI
{
    public static partial class PropertyEditorHelper
    {
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
                            ? metadataProvider.GetCategory(prop) ?? categoryAttr?.Category ?? GetDisplayMetadata(prop, display => display.GetGroupName()) ?? type.Name
                            : categoryAttr?.Category ?? GetDisplayMetadata(prop, display => display.GetGroupName()) ?? type.Name;

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
                        FrameworkElement categoryContainer = stackPanel;
                        if (!useIntegratedLayout)
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
                            categoryContainer = border;
                        }

                        var propertyRows = stackPanel.Children.OfType<DockPanel>().Where(row => row.Tag is PropertyInfo).ToArray();
                        if (!addAdvancedToggle && propertyRows.Any(row => BindingOperations.IsDataBound(row, UIElement.VisibilityProperty)))
                        {
                            var visibility = new MultiBinding { Converter = AnyVisiblePropertyConverter.Instance };
                            foreach (var row in propertyRows)
                                visibility.Bindings.Add(new Binding { Source = row, Path = new PropertyPath(UIElement.VisibilityProperty), Mode = BindingMode.OneWay });
                            categoryContainer.SetBinding(UIElement.VisibilityProperty, visibility);
                        }
                        propertyPanel.Children.Add(categoryContainer);
                    }
                }

                return propertyPanel;
            }

            finally
            {
                visited.Remove(obj);
            }
        }

        private sealed class AnyVisiblePropertyConverter : IMultiValueConverter
        {
            public static AnyVisiblePropertyConverter Instance { get; } = new();
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
                values.Any(value => value is Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
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
    }
}
