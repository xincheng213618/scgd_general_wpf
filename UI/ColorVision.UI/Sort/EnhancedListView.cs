using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.UI.Sorts
{
    /// <summary>
    /// 增强的 ListView 控件，内置排序、过滤和列管理功能
    /// </summary>
    public class EnhancedListView : ListView
    {
        private SortManager<object>? _sortManager;
        private ObservableCollection<GridViewColumnVisibility> _columnVisibilities;
        private CollectionViewSource _viewSource;

        static EnhancedListView()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(EnhancedListView), new FrameworkPropertyMetadata(typeof(EnhancedListView)));
        }

        public EnhancedListView()
        {
            _columnVisibilities = new ObservableCollection<GridViewColumnVisibility>();
            _viewSource = new CollectionViewSource();
            
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
        }

        #region 依赖属性

        /// <summary>
        /// 是否启用自动排序
        /// </summary>
        public static readonly DependencyProperty EnableSortingProperty = 
            DependencyProperty.Register(nameof(EnableSorting), typeof(bool), typeof(EnhancedListView), 
                new PropertyMetadata(true, OnEnableSortingChanged));

        public bool EnableSorting
        {
            get => (bool)GetValue(EnableSortingProperty);
            set => SetValue(EnableSortingProperty, value);
        }

        /// <summary>
        /// 是否启用列管理
        /// </summary>
        public static readonly DependencyProperty EnableColumnManagementProperty = 
            DependencyProperty.Register(nameof(EnableColumnManagement), typeof(bool), typeof(EnhancedListView), 
                new PropertyMetadata(true));

        public bool EnableColumnManagement
        {
            get => (bool)GetValue(EnableColumnManagementProperty);
            set => SetValue(EnableColumnManagementProperty, value);
        }

        /// <summary>
        /// 默认排序属性
        /// </summary>
        public static readonly DependencyProperty DefaultSortPropertyProperty = 
            DependencyProperty.Register(nameof(DefaultSortProperty), typeof(string), typeof(EnhancedListView));

        public string DefaultSortProperty
        {
            get => (string)GetValue(DefaultSortPropertyProperty);
            set => SetValue(DefaultSortPropertyProperty, value);
        }

        /// <summary>
        /// 默认排序方向
        /// </summary>
        public static readonly DependencyProperty DefaultSortDirectionProperty = 
            DependencyProperty.Register(nameof(DefaultSortDirection), typeof(ListSortDirection), typeof(EnhancedListView), 
                new PropertyMetadata(ListSortDirection.Ascending));

        public ListSortDirection DefaultSortDirection
        {
            get => (ListSortDirection)GetValue(DefaultSortDirectionProperty);
            set => SetValue(DefaultSortDirectionProperty, value);
        }

        #endregion

        #region 公共属性

        /// <summary>
        /// 列可见性配置
        /// </summary>
        public ObservableCollection<GridViewColumnVisibility> ColumnVisibilities => _columnVisibilities;

        /// <summary>
        /// 排序管理器
        /// </summary>
        public SortManager<object>? SortManager => _sortManager;

        #endregion

        #region 事件处理

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InitializeFeatures();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CleanupFeatures();
        }

        private static void OnEnableSortingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EnhancedListView listView)
            {
                listView.UpdateSortingFeature((bool)e.NewValue);
            }
        }

        protected override void OnItemsSourceChanged(System.Collections.IEnumerable oldValue, System.Collections.IEnumerable newValue)
        {
            base.OnItemsSourceChanged(oldValue, newValue);
            
            if (newValue is ObservableCollection<object> collection)
            {
                _sortManager = new SortManager<object>(collection);
                ApplyDefaultSort();
            }
        }

        #endregion

        #region 私有方法

        private void InitializeFeatures()
        {
            if (View is GridView gridView)
            {
                SetupColumnManagement(gridView);
                SetupSorting(gridView);
            }
        }

        private void CleanupFeatures()
        {
            // 清理事件订阅等
        }

        private void SetupColumnManagement(GridView gridView)
        {
            if (!EnableColumnManagement) return;

            // 初始化列可见性配置
            GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, _columnVisibilities);

            // 设置右键菜单
            if (ContextMenu == null)
            {
                ContextMenu = new ContextMenu();
            }

            GridViewColumnVisibility.GenContentMenuGridViewColumn(
                ContextMenu, gridView.Columns, _columnVisibilities, this);
        }

        private void SetupSorting(GridView gridView)
        {
            if (!EnableSorting) return;

            // 为每个列添加点击排序功能
            foreach (var column in gridView.Columns)
            {
                if (column.Header is FrameworkElement header)
                {
                    header.MouseLeftButtonUp += (s, e) =>
                    {
                        var binding = column.DisplayMemberBinding as Binding;
                        var propertyName = binding?.Path?.Path ?? column.Header?.ToString();
                        
                        if (!string.IsNullOrEmpty(propertyName))
                        {
                            SortByColumn(propertyName);
                        }
                    };
                }
            }
        }

        private void UpdateSortingFeature(bool enabled)
        {
            // 根据启用状态更新排序功能
            if (View is GridView gridView)
            {
                if (enabled)
                {
                    SetupSorting(gridView);
                }
                else
                {
                    // 移除排序相关的事件处理器
                }
            }
        }

        private void ApplyDefaultSort()
        {
            if (!string.IsNullOrEmpty(DefaultSortProperty) && _sortManager != null)
            {
                _sortManager.ApplySort(DefaultSortProperty, DefaultSortDirection == ListSortDirection.Descending);
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 按指定列排序
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="descending">是否降序</param>
        public void SortByColumn(string propertyName, bool? descending = null)
        {
            _sortManager?.ApplySort(propertyName, descending);
            
            // 更新列头显示
            UpdateColumnHeaders(propertyName, descending ?? false);
        }

        /// <summary>
        /// 智能排序
        /// </summary>
        public void SmartSort(bool descending = false)
        {
            if (ItemsSource is ObservableCollection<object> collection)
            {
                collection.SmartSort(descending);
            }
        }

        /// <summary>
        /// 重置到默认排序
        /// </summary>
        public void ResetToDefaultSort()
        {
            ApplyDefaultSort();
        }

        /// <summary>
        /// 显示/隐藏列
        /// </summary>
        public void SetColumnVisible(string columnName, bool visible)
        {
            var column = _columnVisibilities.FirstOrDefault(c => c.ColumnName?.ToString() == columnName);
            if (column != null)
            {
                column.IsVisible = visible;
            }
        }

        /// <summary>
        /// 自动调整列宽
        /// </summary>
        public void AutoResizeColumns()
        {
            if (View is GridView gridView)
            {
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, _columnVisibilities);
            }
        }

        private void UpdateColumnHeaders(string sortedProperty, bool descending)
        {
            // 更新列头显示排序指示器
            foreach (var column in _columnVisibilities)
            {
                column.IsSortD = column.ColumnName?.ToString() == sortedProperty ? descending : false;
            }
        }

        #endregion
    }
}