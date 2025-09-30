#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ColorVision.UI
{


    /// <summary>
    /// EditConfig.xaml 的交互逻辑
    /// </summary>
    public partial class PropertyEditorWindow : Window
    {
        public event EventHandler Submited;
        public object Config { get; set; }
        public object EditConfig { get; set; }
        public bool IsEdit { get; set; } = true;

        public Dictionary<string, List<PropertyInfo>> categoryGroups { get; set; } = new Dictionary<string, List<PropertyInfo>>();

        ResourceManager? resourceManager;

        public PropertyEditorWindow(object config ,bool isEdit = true)
        {
            Type type = config.GetType();
            var lazyResourceManager = PropertyEditorHelper.ResourceManagerCache.GetOrAdd(type, t => new Lazy<ResourceManager?>(() =>
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
            })
            {

            });
            resourceManager = lazyResourceManager.Value;

            IsEdit = isEdit;
            Config = config;
            InitializeComponent();
            this.ApplyCaption();
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


        public void GenCategoryGroups(object source)
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


        public void DisplayProperties(object obj)
        {
            categoryGroups.Clear();
            GenCategoryGroups(obj);
            
            // Get category order from CategoryOrderAttribute on the class
            var type = obj.GetType();
            var categoryOrderAttrs = type.GetCustomAttributes<CategoryOrderAttribute>().ToList();
            var categoryOrderMap = categoryOrderAttrs.ToDictionary(a => a.Category, a => a.Order);

            // Sort categories: first by order (if specified), then alphabetically
            var sortedCategories = categoryGroups
                .OrderBy(cg => categoryOrderMap.TryGetValue(cg.Key, out var order) ? order : int.MaxValue)
                .ThenBy(cg => cg.Key, StringComparer.Ordinal);

            foreach (var categoryGroup in sortedCategories)
            {
                var border = new Border
                {
                    Background = (Brush)FindResource("GlobalBorderBrush"),
                    BorderThickness = new Thickness(1),
                    BorderBrush = (Brush)FindResource("BorderBrush"),
                    CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(0, 0, 0, 5)
                };
                var stackPanel = new StackPanel { Margin = new Thickness(10,5,10,0) };
                border.Child = stackPanel;
                
                // Sort properties: first by PropertyOrderAttribute, then by name
                var sortedProperties = categoryGroup.Value
                    .OrderBy(p => p.GetCustomAttribute<PropertyOrderAttribute>()?.Order ?? int.MaxValue)
                    .ThenBy(p => PropertyEditorHelper.GetDisplayName(resourceManager, p));

                foreach (var property in sortedProperties)
                {
                    var browsableAttr = property.GetCustomAttribute<BrowsableAttribute>();
                    
                    if (browsableAttr?.Browsable ?? true)
                    {
                        DockPanel dockPanel = new DockPanel();
                        if (property.PropertyType.IsEnum)
                        {
                            dockPanel = PropertyEditorHelper.GenEnumProperties(property, obj);
                        }
                        else if (property.PropertyType == typeof(bool))
                        {
                            dockPanel = PropertyEditorHelper.GenBoolProperties(property, obj);
                        }
                        else if (property.PropertyType == typeof(int?) || property.PropertyType == typeof(int) || property.PropertyType == typeof(float) || property.PropertyType == typeof(float?) || property.PropertyType == typeof(uint) || property.PropertyType == typeof(long) || property.PropertyType == typeof(ulong) || property.PropertyType == typeof(sbyte) || property.PropertyType == typeof(double) || property.PropertyType == typeof(double?) || property.PropertyType == typeof(string))
                        {
                            dockPanel = PropertyEditorHelper.GenTextboxProperties(property, obj);
                        }
                        else if (property.PropertyType == typeof(System.Windows.Rect))
                        {
                            dockPanel = PropertyEditorHelper.GenTextboxProperties(property, obj);
                        }
                        else if (typeof(Brush).IsAssignableFrom(property.PropertyType))
                        {
                            dockPanel = PropertyEditorHelper.GenBrushProperties(property, obj);
                        }
                        else if (typeof(ICommand).IsAssignableFrom(property.PropertyType))
                        {
                            dockPanel = PropertyEditorHelper.GenCommandProperties(property, obj);
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

                        else if (typeof(INotifyPropertyChanged).IsAssignableFrom(property.PropertyType))
                        {
                            // 如果属性是ViewModelBase的子类，递归解析
                            var nestedObj = (INotifyPropertyChanged)property.GetValue(obj);
                            if (nestedObj != null)
                            {
                                stackPanel.Margin = new Thickness(0);
                                stackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(nestedObj));
                                continue;
                            }
                        }
                        else if (property.PropertyType ==typeof(object) && property.GetValue(obj) is INotifyPropertyChanged nestedObj)
                        {
                            stackPanel.Margin = new Thickness(0);
                            stackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(nestedObj));
                            continue;
                        }
                        else
                        {
                            continue;
                        }
                        
                        dockPanel.Margin = new Thickness(0, 0, 0, 5);

                        var VisibleBlindAttr = property.GetCustomAttribute<PropertyVisibilityAttribute>();
                        if (VisibleBlindAttr != null)
                        {
                            var binding = new Binding(VisibleBlindAttr.PropertyName)
                            {
                                Source = obj,
                                Mode = BindingMode.TwoWay
                            };

                            binding.Converter = (IValueConverter)Application.Current.FindResource(VisibleBlindAttr.IsInverted?"bool2VisibilityConverter": "bool2VisibilityConverter1");
                            dockPanel.SetBinding(DockPanel.VisibilityProperty, binding);
                        }
                        stackPanel.Children.Add(dockPanel);
                    }

                }
                if (stackPanel.Children.Count > 0)
                {
                    TreeViewItem treeViewItem = new TreeViewItem() { Header = categoryGroup.Key, Tag = border };
                    treeView.Items.Add(treeViewItem);

                    PropertyPanel.Children.Add(border);
                }
            }
        }


        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if(sender is TreeView treeView && treeView.SelectedItem is TreeViewItem treeViewItem && treeViewItem.Tag is Border obj)
            {
                obj.BringIntoView();
            }
        }
    }
}
