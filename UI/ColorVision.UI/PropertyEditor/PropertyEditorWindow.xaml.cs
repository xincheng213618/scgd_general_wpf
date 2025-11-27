#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI.LogImp;
using log4net.Core;
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
    public enum PropertySortMode
    {
        Default,
        NameAscending,
        NameDescending,
        CategoryAscending,
        CategoryDescending
    }

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
        
        private string searchText = string.Empty;
        private Dictionary<Border, TreeViewItem> borderToTreeViewItem = new Dictionary<Border, TreeViewItem>();
        private PropertySortMode currentSortMode = PropertySortMode.Default;


        public PropertyEditorWindow(object config ,bool isEdit = true)
        {
            Type type = config.GetType();
            IsEdit = isEdit;
            Config = config;
            InitializeComponent();
            this.ApplyCaption();
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;

            // Initialize sort options
            SortComboBox.Items.Add(new ComboBoxItem { Content = "默认排序", Tag = PropertySortMode.Default });
            SortComboBox.Items.Add(new ComboBoxItem { Content = "按名称排序 (升序)", Tag = PropertySortMode.NameAscending });
            SortComboBox.Items.Add(new ComboBoxItem { Content = "按名称排序 (降序)", Tag = PropertySortMode.NameDescending });
            SortComboBox.Items.Add(new ComboBoxItem { Content = "按分类排序 (升序)", Tag = PropertySortMode.CategoryAscending });
            SortComboBox.Items.Add(new ComboBoxItem { Content = "按分类排序 (降序)", Tag = PropertySortMode.CategoryDescending });
            SortComboBox.SelectedIndex = 0;

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
        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (!IsEdit)
                EditConfig.CopyTo(Config);
            Submited?.Invoke(sender, new EventArgs());
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (!IsEdit)
            {
                Config.CopyTo(EditConfig);
                PropertyPanel.Children.Clear();
                SearchBox.Text = string.Empty;  // Clear search when resetting
                DisplayProperties(EditConfig);
            }
            else
            {
                SearchBox.Text = string.Empty;  // Clear search when resetting
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
                string category = categoryAttr?.Category ?? type.Name;
                if (!categoryGroups.TryGetValue(category, out List<PropertyInfo>? value))
                {
                    categoryGroups.Add(category, new List<PropertyInfo>() { property });
                }
                else
                {
                    value.Add(property);
                }
            }
        }


        public void DisplayProperties(object obj)
        {
            categoryGroups.Clear();
            borderToTreeViewItem.Clear();
            GenCategoryGroups(obj);


            foreach (var categoryGroup in categoryGroups)
            {
                var border = new Border
                {
                    Background = (Brush)FindResource("GlobalBorderBrush"),
                    BorderThickness = new Thickness(1),
                    BorderBrush = (Brush)FindResource("BorderBrush"),
                    CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(0, 0, 0, 5),
                    Tag = categoryGroup.Key  // Tag with category name for search
                };
                var stackPanel = new StackPanel { Margin = new Thickness(10,5,10,0) };
                border.Child = stackPanel;


                var categoryHeader = new TextBlock
                {
                    Text = categoryGroup.Key,
                    FontWeight = FontWeights.Bold,
                    Foreground = PropertyEditorHelper.GlobalTextBrush,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                stackPanel.Children.Add(categoryHeader);

                foreach (var property in categoryGroup.Value)
                {
                    var browsableAttr = property.GetCustomAttribute<BrowsableAttribute>();
                    
                    if (browsableAttr?.Browsable ?? true)
                    {
                        DockPanel dockPanel = null;

                        var editorAttr = property.GetCustomAttribute<PropertyEditorTypeAttribute>();
                        if (editorAttr?.EditorType != null)
                        {
                            try
                            {
                                var editor = PropertyEditorHelper.GetOrCreateEditor(editorAttr.EditorType);
                                dockPanel = editor.GenProperties(property, obj);
                            }
                            catch (Exception)
                            {
                            }
                        }

                        if (dockPanel == null)
                        {
                            Type? editorType = null;
                            editorType = PropertyEditorHelper.GetEditorTypeForPropertyType(property.PropertyType);
                            if (editorType != null)
                            {
                                try
                                {
                                    var editor = PropertyEditorHelper.GetOrCreateEditor(editorType);
                                    dockPanel = editor.GenProperties(property, obj);
                                }
                                catch (Exception)
                                {
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
                            else
                            {
                                continue;
                            }
                        }

                        dockPanel.Margin = new Thickness(0, 0, 0, 5);
                        dockPanel.Tag = property;  // Tag with PropertyInfo for search

                        var VisibleBlindAttr = property.GetCustomAttribute<PropertyVisibilityAttribute>();
                        if (VisibleBlindAttr != null)
                        {
                            var binding = new Binding(VisibleBlindAttr.PropertyName)
                            {
                                Source = obj,
                                Mode = BindingMode.OneWay
                            };

                            // If ExpectedValue is set, this is an enum binding
                            if (VisibleBlindAttr.ExpectedValue != null)
                            {
                                binding.Converter = (IValueConverter)Application.Current.FindResource(
                                    VisibleBlindAttr.IsInverted ? "enum2VisibilityConverter1" : "enum2VisibilityConverter");
                                binding.ConverterParameter = VisibleBlindAttr.ExpectedValue;
                            }
                            else
                            {
                                // Boolean binding - Corrected logic:
                                // IsInverted=false: use standard converter (true→Visible)
                                // IsInverted=true: use reversed converter (true→Collapsed)
                                binding.Converter = (IValueConverter)Application.Current.FindResource(
                                    VisibleBlindAttr.IsInverted ? "bool2VisibilityConverter1" : "bool2VisibilityConverter");
                            }
                            
                            dockPanel.SetBinding(DockPanel.VisibilityProperty, binding);
                        }
                        stackPanel.Children.Add(dockPanel);
                    }
                }

                if (stackPanel.Children.Count > 1)
                {
                    TreeViewItem treeViewItem = new TreeViewItem() { Header = categoryGroup.Key, Tag = border };
                    treeView.Items.Add(treeViewItem);
                    borderToTreeViewItem[border] = treeViewItem;
                    PropertyPanel.Children.Add(border);
                }
            }

            if (treeView.Items.Count == 1)
            {
                treeView.Visibility = Visibility.Collapsed;
            }
            else
            {
                treeView.Visibility = Visibility.Visible;
            }
        }


        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if(sender is TreeView treeView && treeView.SelectedItem is TreeViewItem treeViewItem && treeViewItem.Tag is Border obj)
            {
                obj.BringIntoView();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            searchText = SearchBox.Text ?? string.Empty;
            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            // Check if UI is initialized (avoid race condition)
            if (borderToTreeViewItem.Count == 0)
                return;
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Show all items when search is empty
                foreach (UIElement child in PropertyPanel.Children)
                {
                    child.Visibility = Visibility.Visible;
                    if (child is Border border && border.Child is StackPanel stackPanel)
                    {
                        foreach (UIElement item in stackPanel.Children)
                        {
                            item.Visibility = Visibility.Visible;
                        }
                    }
                }
                
                // Show all tree view items and restore TreeView visibility
                foreach (TreeViewItem item in treeView.Items)
                {
                    item.Visibility = Visibility.Visible;
                }
                
                // Restore TreeView visibility based on item count
                if (treeView.Items.Count == 1)
                {
                    treeView.Visibility = Visibility.Collapsed;
                }
                else
                {
                    treeView.Visibility = Visibility.Visible;
                }
                return;
            }

            // Apply search filter
            int visibleCategories = 0;
            foreach (UIElement child in PropertyPanel.Children)
            {
                if (child is Border border && border.Child is StackPanel stackPanel && border.Tag is string category)
                {
                    bool categoryVisible = false;
                    
                    // Check if category name matches
                    if (category.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    {
                        categoryVisible = true;
                        // Show all children if category matches
                        foreach (UIElement item in stackPanel.Children)
                        {
                            item.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        // Filter individual properties
                        foreach (UIElement item in stackPanel.Children)
                        {
                            if (item is DockPanel dockPanel && dockPanel.Tag is PropertyInfo property)
                            {
                                bool matches = MatchesSearch(property);
                                item.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
                                if (matches) categoryVisible = true;
                            }
                            else if (item is TextBlock textBlock)
                            {
                                // Keep category header visible
                                item.Visibility = Visibility.Visible;
                            }
                        }
                    }

                    border.Visibility = categoryVisible ? Visibility.Visible : Visibility.Collapsed;
                    if (categoryVisible) visibleCategories++;
                    
                    // Update corresponding tree view item using dictionary
                    if (borderToTreeViewItem.TryGetValue(border, out TreeViewItem? treeItem))
                    {
                        treeItem.Visibility = categoryVisible ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
            
            // Update TreeView visibility based on visible categories during search
            if (visibleCategories <= 1)
            {
                treeView.Visibility = Visibility.Collapsed;
            }
            else
            {
                treeView.Visibility = Visibility.Visible;
            }
        }

        private bool MatchesSearch(PropertyInfo property)
        {
            // Check DisplayName
            var displayNameAttr = property.GetCustomAttribute<DisplayNameAttribute>();
            string displayName = displayNameAttr?.DisplayName ?? property.Name;
            if (displayName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                return true;

            // Check property name
            if (property.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                return true;

            // Check Description
            var descAttr = property.GetCustomAttribute<DescriptionAttribute>();
            if (descAttr?.Description != null && descAttr.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                return true;

            // Check Category
            var categoryAttr = property.GetCustomAttribute<CategoryAttribute>();
            if (categoryAttr?.Category != null && categoryAttr.Category.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SortComboBox.SelectedItem is ComboBoxItem item && item.Tag is PropertySortMode sortMode)
            {
                currentSortMode = sortMode;
                ApplySorting();
            }
        }

        private void ApplySorting()
        {
            if (categoryGroups.Count == 0)
                return;

            switch (currentSortMode)
            {
                case PropertySortMode.NameAscending:
                    SortPropertiesByName(ascending: true);
                    break;
                case PropertySortMode.NameDescending:
                    SortPropertiesByName(ascending: false);
                    break;
                case PropertySortMode.CategoryAscending:
                    SortCategoriesByName(ascending: true);
                    break;
                case PropertySortMode.CategoryDescending:
                    SortCategoriesByName(ascending: false);
                    break;
                case PropertySortMode.Default:
                default:
                    // Restore default order - regenerate display
                    PropertyPanel.Children.Clear();
                    treeView.Items.Clear();
                    borderToTreeViewItem.Clear();
                    DisplayProperties(IsEdit ? Config : EditConfig);
                    ApplySearchFilter();
                    return;
            }

            // After sorting, reapply search filter if active
            ApplySearchFilter();
        }

        private void SortPropertiesByName(bool ascending)
        {
            // Sort properties within each category
            foreach (var category in categoryGroups.Keys.ToList())
            {
                var sortedProperties = ascending
                    ? categoryGroups[category].OrderBy(p => p.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? p.Name).ToList()
                    : categoryGroups[category].OrderByDescending(p => p.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? p.Name).ToList();
                
                categoryGroups[category] = sortedProperties;
            }

            // Rebuild the display
            RebuildDisplay();
        }

        private void SortCategoriesByName(bool ascending)
        {
            // Create a sorted dictionary of categories
            var sortedCategories = ascending
                ? categoryGroups.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                : categoryGroups.OrderByDescending(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            categoryGroups = sortedCategories;

            // Rebuild the display
            RebuildDisplay();
        }

        private void RebuildDisplay()
        {
            PropertyPanel.Children.Clear();
            treeView.Items.Clear();
            borderToTreeViewItem.Clear();

            var source = IsEdit ? Config : EditConfig;

            foreach (var categoryGroup in categoryGroups)
            {
                var border = new Border
                {
                    Background = (Brush)FindResource("GlobalBorderBrush"),
                    BorderThickness = new Thickness(1),
                    BorderBrush = (Brush)FindResource("BorderBrush"),
                    CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(0, 0, 0, 5),
                    Tag = categoryGroup.Key
                };
                var stackPanel = new StackPanel { Margin = new Thickness(10, 5, 10, 0) };
                border.Child = stackPanel;

                var categoryHeader = new TextBlock
                {
                    Text = categoryGroup.Key,
                    FontWeight = FontWeights.Bold,
                    Foreground = PropertyEditorHelper.GlobalTextBrush,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                stackPanel.Children.Add(categoryHeader);

                foreach (var property in categoryGroup.Value)
                {
                    var browsableAttr = property.GetCustomAttribute<BrowsableAttribute>();

                    if (browsableAttr?.Browsable ?? true)
                    {
                        DockPanel dockPanel = null;

                        var editorAttr = property.GetCustomAttribute<PropertyEditorTypeAttribute>();
                        if (editorAttr?.EditorType != null)
                        {
                            try
                            {
                                var editor = PropertyEditorHelper.GetOrCreateEditor(editorAttr.EditorType);
                                dockPanel = editor.GenProperties(property, source);
                            }
                            catch (Exception)
                            {
                            }
                        }

                        if (dockPanel == null)
                        {
                            Type? editorType = null;
                            editorType = PropertyEditorHelper.GetEditorTypeForPropertyType(property.PropertyType);
                            if (editorType != null)
                            {
                                try
                                {
                                    var editor = PropertyEditorHelper.GetOrCreateEditor(editorType);
                                    dockPanel = editor.GenProperties(property, source);
                                }
                                catch (Exception)
                                {
                                    continue;
                                }
                            }
                            else if (property.PropertyType == typeof(object))
                            {
                                stackPanel.Margin = new Thickness(5);
                                StackPanel stackPanel1 = PropertyEditorHelper.GenPropertyEditorControl(property.GetValue(source));
                                if (stackPanel1.Children.Count == 1 && stackPanel1.Children[0] is Border border1 && border1.Child is StackPanel stackPanel2 && stackPanel2.Children.Count != 0)
                                {
                                    stackPanel.Children.Add(stackPanel1);
                                }
                                continue;
                            }
                            else if (typeof(INotifyPropertyChanged).IsAssignableFrom(property.PropertyType))
                            {
                                // 如果属性是ViewModelBase的子类，递归解析
                                var nestedObj = (INotifyPropertyChanged)property.GetValue(source);
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
                            else
                            {
                                continue;
                            }
                        }

                        dockPanel.Margin = new Thickness(0, 0, 0, 5);
                        dockPanel.Tag = property;

                        var VisibleBlindAttr = property.GetCustomAttribute<PropertyVisibilityAttribute>();
                        if (VisibleBlindAttr != null)
                        {
                            var binding = new Binding(VisibleBlindAttr.PropertyName)
                            {
                                Source = source,
                                Mode = BindingMode.OneWay
                            };

                            // If ExpectedValue is set, this is an enum binding
                            if (VisibleBlindAttr.ExpectedValue != null)
                            {
                                binding.Converter = (IValueConverter)Application.Current.FindResource(
                                    VisibleBlindAttr.IsInverted ? "enum2VisibilityConverter1" : "enum2VisibilityConverter");
                                binding.ConverterParameter = VisibleBlindAttr.ExpectedValue;
                            }
                            else
                            {
                                // Boolean binding - Corrected logic:
                                // IsInverted=false: use standard converter (true→Visible)
                                // IsInverted=true: use reversed converter (true→Collapsed)
                                binding.Converter = (IValueConverter)Application.Current.FindResource(
                                    VisibleBlindAttr.IsInverted ? "bool2VisibilityConverter1" : "bool2VisibilityConverter");
                            }
                            
                            dockPanel.SetBinding(DockPanel.VisibilityProperty, binding);
                        }
                        stackPanel.Children.Add(dockPanel);
                    }
                }

                if (stackPanel.Children.Count > 1)
                {
                    TreeViewItem treeViewItem = new TreeViewItem() { Header = categoryGroup.Key, Tag = border };
                    treeView.Items.Add(treeViewItem);
                    borderToTreeViewItem[border] = treeViewItem;
                    PropertyPanel.Children.Add(border);
                }
            }

            if (treeView.Items.Count == 1)
            {
                treeView.Visibility = Visibility.Collapsed;
            }
            else
            {
                treeView.Visibility = Visibility.Visible;
            }
        }
    }
}
