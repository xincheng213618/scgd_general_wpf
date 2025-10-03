using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.UI.Sorts
{
    /// <summary>
    /// ��ǿ�� ListView �ؼ����������򡢹��˺��й�����
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

        #region ��������

        /// <summary>
        /// �Ƿ������Զ�����
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
        /// �Ƿ������й���
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
        /// Ĭ����������
        /// </summary>
        public static readonly DependencyProperty DefaultSortPropertyProperty = 
            DependencyProperty.Register(nameof(DefaultSortProperty), typeof(string), typeof(EnhancedListView));

        public string DefaultSortProperty
        {
            get => (string)GetValue(DefaultSortPropertyProperty);
            set => SetValue(DefaultSortPropertyProperty, value);
        }

        /// <summary>
        /// Ĭ��������
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

        #region ��������

        /// <summary>
        /// �пɼ�������
        /// </summary>
        public ObservableCollection<GridViewColumnVisibility> ColumnVisibilities => _columnVisibilities;

        /// <summary>
        /// ���������
        /// </summary>
        public SortManager<object>? SortManager => _sortManager;

        #endregion

        #region �¼�����

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

        #region ˽�з���

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
            // �����¼����ĵ�
        }

        private void SetupColumnManagement(GridView gridView)
        {
            if (!EnableColumnManagement) return;

            // ��ʼ���пɼ�������
            GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, _columnVisibilities);

            // �����Ҽ��˵�
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

            // Ϊÿ������ӵ��������
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
            // ��������״̬����������
            if (View is GridView gridView)
            {
                if (enabled)
                {
                    SetupSorting(gridView);
                }
                else
                {
                    // �Ƴ�������ص��¼�������
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

        #region ��������

        /// <summary>
        /// ��ָ��������
        /// </summary>
        /// <param name="propertyName">������</param>
        /// <param name="descending">�Ƿ���</param>
        public void SortByColumn(string propertyName, bool? descending = null)
        {
            _sortManager?.ApplySort(propertyName, descending);
            
            // ������ͷ��ʾ
            UpdateColumnHeaders(propertyName, descending ?? false);
        }

        /// <summary>
        /// ��������
        /// </summary>
        public void SmartSort(bool descending = false)
        {
            if (ItemsSource is ObservableCollection<object> collection)
            {
                collection.SmartSort(descending);
            }
        }

        /// <summary>
        /// ���õ�Ĭ������
        /// </summary>
        public void ResetToDefaultSort()
        {
            ApplyDefaultSort();
        }

        /// <summary>
        /// ��ʾ/������
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
        /// �Զ������п�
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
            // ������ͷ��ʾ����ָʾ��
            foreach (var column in _columnVisibilities)
            {
                column.IsSortD = column.ColumnName?.ToString() == sortedProperty ? descending : false;
            }
        }

        #endregion
    }
}