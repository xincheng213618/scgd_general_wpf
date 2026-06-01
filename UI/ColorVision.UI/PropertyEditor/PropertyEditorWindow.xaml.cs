#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI.LogImp;
using log4net.Core;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows;
using ColorVision.UI.Properties;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ColorVision.UI
{
    public class TreeViewItemMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double num = 0.0;
            UIElement uIElement = value as TreeViewItem;
            while (uIElement != null && uIElement.GetType() != typeof(TreeView))
            {
                uIElement = (UIElement)VisualTreeHelper.GetParent(uIElement);
                if (uIElement is TreeViewItem)
                {
                    num += 12.0;
                }
            }

            return new Thickness(num, 0.0, 0.0, 0.0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

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

        public object ConfigCopy { get; set; }

        public object Config { get; set; }
        public object EditConfig { get; set; }

        public Dictionary<string, List<PropertyInfo>> categoryGroups { get; set; } = new Dictionary<string, List<PropertyInfo>>();
        
        private string searchText = string.Empty;
        private PropertySortMode currentSortMode = PropertySortMode.Default;
        private object CurrentSource => isEdit ? Config : EditConfig;

        /// <summary>
        /// Observable collection for TreeView binding
        /// </summary>
        public ObservableCollection<PropertyTreeNode> TreeNodes { get; } = new ObservableCollection<PropertyTreeNode>();

        private bool isEdit = true;
        public PropertyEditorWindow(object config ,bool isEdit = true)
        {
            this.isEdit = isEdit;
            Config = config;
            InitializeComponent();
            this.ApplyCaption();
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = this;

            // Initialize sort options
            SortComboBox.Items.Add(new ComboBoxItem { Content = Properties.Resources.PropEditor_SortDefault, Tag = PropertySortMode.Default });
            SortComboBox.Items.Add(new ComboBoxItem { Content = Properties.Resources.PropEditor_SortNameAsc, Tag = PropertySortMode.NameAscending });
            SortComboBox.Items.Add(new ComboBoxItem { Content = Properties.Resources.PropEditor_SortNameDesc, Tag = PropertySortMode.NameDescending });
            SortComboBox.Items.Add(new ComboBoxItem { Content = Properties.Resources.PropEditor_SortCategoryAsc, Tag = PropertySortMode.CategoryAscending });
            SortComboBox.Items.Add(new ComboBoxItem { Content = Properties.Resources.PropEditor_SortCategoryDesc, Tag = PropertySortMode.CategoryDescending });
            SortComboBox.SelectedIndex = 0;


            EditConfig = Config.Clone();
            DisplayProperties(CurrentSource);
        }
        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (!isEdit)
            {
                EditConfig.CopyTo(Config);
            }
            Submited?.Invoke(sender, new EventArgs());
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty;
            if (!isEdit)
            {
                Config.CopyTo(EditConfig);
            }
            else
            {
                EditConfig.CopyTo(Config);
            }

            DisplayProperties(CurrentSource);
        }

        private void ResetToFactory_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty;
            if (!isEdit)
            {
                EditConfig.Reset();
            }
            else
            {
                Config.Reset();
            }

            DisplayProperties(CurrentSource);
  
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

        public void GenCategoryGroups(object source)
        {
            var t = source.GetType();

            // 1. 获取属性
            var allProps = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.CanRead && p.CanWrite);

            // 2. 【关键修改】进行排序
            // GetInheritanceDepth 越小，说明越靠近基类 (object -> BaseConfig -> DeviceServiceConfig -> ConfigPG)
            // 如果您想要“原始信息”（基类）在最前面，请使用 OrderBy
            // 如果您想要“最上层”（派生类）在最前面，请使用 OrderByDescending
            var sortedProps = allProps.OrderBy(p => GetInheritanceDepth(p.DeclaringType ?? t));

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



        public void DisplayProperties(object obj)
        {
            categoryGroups.Clear();
            TreeNodes.Clear();
            GenCategoryGroups(obj);
            RebuildDisplay(obj);
        }

        private void RebuildDisplay()
        {
            RebuildDisplay(CurrentSource);
        }

        private void RebuildDisplay(object source)
        {
            PropertyPanel.Children.Clear();
            TreeNodes.Clear();

            foreach (var categoryGroup in categoryGroups)
            {
                var border = CreateCategoryBorder(categoryGroup.Key, out var stackPanel);
                var treeNode = new PropertyTreeNode(categoryGroup.Key, border);

                foreach (var property in categoryGroup.Value)
                {
                    TryAddPropertyEditor(source, property, stackPanel, treeNode);
                }

                if (stackPanel.Children.Count > 1)
                {
                    TreeNodes.Add(treeNode);
                    PropertyPanel.Children.Add(border);
                }
            }

            UpdateTreeViewVisibility();
        }

        private Border CreateCategoryBorder(string category, out StackPanel stackPanel)
        {
            var border = new Border
            {
                Background = (Brush)FindResource("GlobalBorderBrush"),
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(0, 0, 0, 5),
                Tag = category
            };

            stackPanel = new StackPanel { Margin = new Thickness(10, 5, 10, 0) };
            stackPanel.Children.Add(new TextBlock
            {
                Text = category,
                FontWeight = FontWeights.Bold,
                Foreground = PropertyEditorHelper.GlobalTextBrush,
                Margin = new Thickness(0, 0, 0, 5)
            });

            border.Child = stackPanel;
            return border;
        }

        private bool TryAddPropertyEditor(object source, PropertyInfo property, StackPanel stackPanel, PropertyTreeNode treeNode)
        {
            if (PropertyEditorHelper.HasEditorForProperty(property))
            {
                if (PropertyEditorHelper.TryCreatePropertyDockPanel(property, source, out var dockPanel))
                {
                    stackPanel.Children.Add(dockPanel);
                    return true;
                }

                return TryAddNestedPropertyEditor(source, property, stackPanel, treeNode);
            }

            return TryAddNestedPropertyEditor(source, property, stackPanel, treeNode);
        }

        private bool TryAddNestedPropertyEditor(object source, PropertyInfo property, StackPanel stackPanel, PropertyTreeNode treeNode)
        {
            if (!PropertyEditorHelper.TryCreateNestedPropertyPanel(property, source, out var nestedPanel))
            {
                return false;
            }

            var rm = PropertyEditorHelper.GetResourceManager(source);
            string displayName = PropertyEditorHelper.GetDisplayName(rm, property);

            nestedPanel.Tag = property;
            stackPanel.Margin = new Thickness(5);
            stackPanel.Children.Add(nestedPanel);
            treeNode.Children.Add(new PropertyTreeNode(displayName, nestedPanel));
            return true;
        }

        private void UpdateTreeViewVisibility()
        {
            treeView.Visibility = ShouldShowTreeView() ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool ShouldShowTreeView()
        {
            int visibleRootCount = TreeNodes.Count(node => node.IsVisible);
            return visibleRootCount > 1 || TreeNodes.Any(HasVisibleChildNode);
        }

        private static bool HasVisibleChildNode(PropertyTreeNode node)
        {
            return node.IsVisible && node.Children.Any(child => child.IsVisible || HasVisibleChildNode(child));
        }


        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Handle selection for PropertyTreeNode (data binding approach)
            if (e.NewValue is PropertyTreeNode node && node.AssociatedBorder != null)
            {
                node.AssociatedBorder.BringIntoView();
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
            if (TreeNodes.Count == 0)
                return;
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Show all items when search is empty
                ShowAllItemsRecursively(PropertyPanel);
                
                // Show all tree nodes
                foreach (var node in TreeNodes)
                {
                    node.ShowAll();
                }
                
                UpdateTreeViewVisibility();
                return;
            }

            // Apply search filter
            foreach (UIElement child in PropertyPanel.Children)
            {
                if (child is Border border && border.Child is StackPanel stackPanel && border.Tag is string category)
                {
                    bool categoryVisible = FilterStackPanelRecursively(stackPanel, category);

                    border.Visibility = categoryVisible ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            
            // Sync tree nodes visibility from borders
            foreach (var node in TreeNodes)
            {
                node.SyncVisibilityFromBorder();
            }
            
            UpdateTreeViewVisibility();
        }

        private void ShowAllItemsRecursively(Panel panel)
        {
            foreach (UIElement child in panel.Children)
            {
                child.Visibility = Visibility.Visible;
                if (child is Border border && border.Child is Panel childPanel)
                {
                    ShowAllItemsRecursively(childPanel);
                }
                else if (child is Panel nestedPanel)
                {
                    ShowAllItemsRecursively(nestedPanel);
                }
            }
        }

        private bool FilterStackPanelRecursively(StackPanel stackPanel, string category)
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
                    if (item is Border nestedBorder && nestedBorder.Child is Panel nestedBorderPanel)
                    {
                        ShowAllItemsRecursively(nestedBorderPanel);
                    }
                    else if (item is StackPanel nestedContainer)
                    {
                        ShowAllItemsRecursively(nestedContainer);
                    }
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
                    else if (item is Border nestedBorder && nestedBorder.Child is StackPanel nestedStackPanel)
                    {
                        bool nestedVisible = FilterBorderRecursively(nestedBorder, nestedStackPanel);
                        item.Visibility = nestedVisible ? Visibility.Visible : Visibility.Collapsed;
                        if (nestedVisible) categoryVisible = true;
                    }
                    else if (item is StackPanel nestedContainer)
                    {
                        // Handle nested content - search in nested panels
                        bool nestedVisible = FilterNestedContainerRecursively(nestedContainer);
                        item.Visibility = nestedVisible ? Visibility.Visible : Visibility.Collapsed;
                        if (nestedVisible) categoryVisible = true;
                    }
                }
            }
            
            return categoryVisible;
        }

        private bool FilterNestedContainerRecursively(StackPanel container)
        {
            if (BorderTagMatchesSearch(container.Tag))
            {
                ShowAllItemsRecursively(container);
                return true;
            }

            bool anyVisible = false;
            
            foreach (UIElement child in container.Children)
            {
                if (child is Border nestedBorder && nestedBorder.Child is StackPanel nestedStackPanel)
                {
                    bool nestedVisible = FilterBorderRecursively(nestedBorder, nestedStackPanel);
                    child.Visibility = nestedVisible ? Visibility.Visible : Visibility.Collapsed;
                    
                    if (nestedVisible) anyVisible = true;
                }
                else if (child is StackPanel nestedPanel)
                {
                    bool nestedVisible = FilterNestedContainerRecursively(nestedPanel);
                    child.Visibility = nestedVisible ? Visibility.Visible : Visibility.Collapsed;
                    if (nestedVisible) anyVisible = true;
                }
            }
            
            return anyVisible;
        }

        private bool FilterBorderRecursively(Border border, StackPanel stackPanel)
        {
            if (BorderTagMatchesSearch(border.Tag))
            {
                ShowAllItemsRecursively(stackPanel);
                return true;
            }

            string category = border.Tag as string ?? string.Empty;
            return FilterStackPanelRecursively(stackPanel, category);
        }

        private bool BorderTagMatchesSearch(object tag)
        {
            if (tag is string category)
            {
                return category.Contains(searchText, StringComparison.OrdinalIgnoreCase);
            }

            if (tag is PropertyInfo property)
            {
                return MatchesSearch(property);
            }

            return false;
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
                    DisplayProperties(CurrentSource);
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

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }
    }
}
