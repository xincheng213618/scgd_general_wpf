using ColorVision.Common.MVVM;
using ColorVision.UI.Extension;
using ColorVision.UI.LogImp;
using log4net.Core;
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
            public Brush GlobalTextBrush { get; }
            public Brush GlobalBorderBrush { get; }
            public Brush BorderBrush { get; }
            public Style ButtonCommandStyle { get; }
            public Style ComboBoxSmallStyle { get; }
            public Style TextBoxSmallStyle { get; }
            public IValueConverter Bool2VisibilityConverter { get; }

            public ResourceCache()
            {
                var app = Application.Current ?? throw new InvalidOperationException(Properties.Resources.ApplicationCurrentNotInitialized);

                GlobalTextBrush = (Brush)app.FindResource("GlobalTextBrush");
                GlobalBorderBrush = (Brush)app.FindResource("GlobalBorderBrush");
                BorderBrush = (Brush)app.FindResource("BorderBrush");
                ButtonCommandStyle = (Style)app.FindResource("ButtonCommand");
                ComboBoxSmallStyle = (Style)app.FindResource("ComboBox.Small");
                TextBoxSmallStyle = (Style)app.FindResource("TextBox.Small");
                Bool2VisibilityConverter = app.TryFindResource("bool2VisibilityConverter") as IValueConverter
                    ?? throw new InvalidOperationException(Properties.Resources.Bool2VisibilityConverterNotFound);
            }
        }

        public static Brush GlobalTextBrush => Resources.Value.GlobalTextBrush;
        public static Brush GlobalBorderBrush => Resources.Value.GlobalBorderBrush;
        public static Brush BorderBrush => Resources.Value.BorderBrush;
        public static Style ButtonCommandStyle => Resources.Value.ButtonCommandStyle;
        public static Style ComboBoxSmallStyle => Resources.Value.ComboBoxSmallStyle;
        public static Style TextBoxSmallStyle => Resources.Value.TextBoxSmallStyle;
        public static IValueConverter Bool2VisibilityConverter => Resources.Value.Bool2VisibilityConverter;


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
                }
            }

            var propertyPanel = new StackPanel();
            CollectProperties(obj);
            
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


                var categoryHeader = new TextBlock
                {
                    Text = categoryGroup.Key,
                    FontWeight = FontWeights.Bold,
                    Foreground = GlobalTextBrush,
                    Margin = new Thickness(0, 0, 0, 5)
                };
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
                Foreground = GlobalTextBrush,
                ToolTip = string.IsNullOrWhiteSpace(desc) ? null : desc
            };
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
    }
}