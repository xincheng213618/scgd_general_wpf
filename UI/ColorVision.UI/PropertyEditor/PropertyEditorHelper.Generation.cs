#pragma warning disable CA1707,CA1852,CS8601
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.UI
{
    public static partial class PropertyEditorHelper
    {
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
    }
}
