using ColorVision.Common.Utilities;
using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.UI.Sorts
{
    /// <summary>
    /// 2014.111.25 这个模块可以使用反射来做，效果和代码都会更简洁
    /// 现已集成通用排序功能，不再强制要求实现特定接口
    /// </summary>

    public static class GridViewColumnVisibilityExtension
    {
        public static void AddGridViewColumn(this ObservableCollection<GridViewColumnVisibility> gridViewColumnVisibilitys, GridViewColumnCollection gridViewColumns) => GridViewColumnVisibility.AddGridViewColumn(gridViewColumns, gridViewColumnVisibilitys);
        public static void AdjustGridViewColumn(this ObservableCollection<GridViewColumnVisibility> gridViewColumnVisibilitys, GridViewColumnCollection gridViewColumns) => GridViewColumnVisibility.AdjustGridViewColumn(gridViewColumns, gridViewColumnVisibilitys);
        public static void AdjustGridViewColumnAuto(this ObservableCollection<GridViewColumnVisibility> gridViewColumnVisibilitys, GridViewColumnCollection gridViewColumns) => GridViewColumnVisibility.AdjustGridViewColumnAuto(gridViewColumns, gridViewColumnVisibilitys);
        
        public static void CopyToGridView(this ObservableCollection<GridViewColumnVisibility> source, ObservableCollection<GridViewColumnVisibility> target)
        {
            // 使用 Lookup 来处理可能的重复项
            var targetLookup = target.ToLookup(item => item.ColumnName);

            foreach (var sourceItem in source)
            {
                foreach (var targetItem in targetLookup[sourceItem.ColumnName])
                {
                    targetItem.IsVisible = sourceItem.IsVisible;
                    targetItem.IsSortD = sourceItem.IsSortD;
                }
            }
        }

        /// <summary>
        /// 对关联的 ListView 数据源进行排序（通用版本）
        /// </summary>
        /// <param name="gridViewColumnVisibilitys">列可见性配置</param>
        /// <param name="listView">目标 ListView</param>
        /// <param name="propertyName">排序属性名</param>
        /// <param name="descending">是否降序</param>
        public static void SortListViewData(this ObservableCollection<GridViewColumnVisibility> gridViewColumnVisibilitys, ListView listView, string propertyName, bool descending = false)
        {
            if (listView?.ItemsSource is not IList sourceList || sourceList.Count == 0)
                return;

            // 使用反射获取集合类型
            var itemType = sourceList.GetType().GetGenericArguments().FirstOrDefault();
            if (itemType == null && sourceList.Count > 0)
            {
                itemType = sourceList[0]?.GetType();
            }
            if (itemType == null) return;

            // 使用通用排序方法
            try
            {
                var sortMethod = typeof(UniversalSortExtensions).GetMethod("SortBy", new[] { typeof(ObservableCollection<>).MakeGenericType(itemType), typeof(string), typeof(bool) });
                if (sortMethod != null && sourceList.GetType().IsGenericType)
                {
                    var genericSortMethod = sortMethod.MakeGenericMethod(itemType);
                    genericSortMethod.Invoke(null, new object[] { sourceList, propertyName, descending });
                    
                    // 更新排序状态
                    foreach (var column in gridViewColumnVisibilitys)
                    {
                        column.IsSortD = column.ColumnName?.ToString() == propertyName ? descending : false;
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果反射失败，尝试直接使用 ObservableCollection<object>
                if (sourceList is ObservableCollection<object> objCollection)
                {
                    objCollection.SortBy(propertyName, descending);
                }
            }
        }

        /// <summary>
        /// 智能排序：自动检测合适的排序属性
        /// </summary>
        public static void SmartSort(this ObservableCollection<GridViewColumnVisibility> gridViewColumnVisibilitys, ListView listView, bool descending = false)
        {
            if (listView?.ItemsSource is not IList sourceList || sourceList.Count == 0)
                return;

            var itemType = sourceList.GetType().GetGenericArguments().FirstOrDefault();
            if (itemType == null && sourceList.Count > 0)
            {
                itemType = sourceList[0]?.GetType();
            }
            if (itemType == null) return;

            try
            {
                var smartSortMethod = typeof(UniversalSortExtensions).GetMethod("SmartSort");
                if (smartSortMethod != null && sourceList.GetType().IsGenericType)
                {
                    var genericMethod = smartSortMethod.MakeGenericMethod(itemType);
                    genericMethod.Invoke(null, new object[] { sourceList, descending });
                }
            }
            catch (Exception ex)
            {
                // 如果反射失败，尝试直接使用 ObservableCollection<object>
                if (sourceList is ObservableCollection<object> objCollection)
                {
                    objCollection.SmartSort(descending);
                }
            }
        }
    }

   // https://stackoverflow.com/questions/747872/wpf-displaying-a-context-menu-for-a-gridviews-items
    public class GridViewColumnVisibility : ViewModelBase
    {
        public static void AddGridViewColumn(GridViewColumnCollection gridViewColumns, ObservableCollection<GridViewColumnVisibility> gridViewColumnVisibilitys)
        {
            gridViewColumnVisibilitys ??= new ObservableCollection<GridViewColumnVisibility>();
            foreach (var item in gridViewColumns)
                gridViewColumnVisibilitys.Add(new GridViewColumnVisibility() { ColumnName = item.Header, GridViewColumn = item, IsVisible = true });
        }

        public static void AdjustGridViewColumn(GridViewColumnCollection gridViewColumns, ObservableCollection<GridViewColumnVisibility> gridViewColumnVisibilitys)
        {
            ///HashSet效率更高
            var invisibleColumns = new HashSet<GridViewColumn>(gridViewColumnVisibilitys.Where(x => !x.IsVisible).Select(x => x.GridViewColumn));

            gridViewColumns.RemoveAll(column => invisibleColumns.Contains(column));
            var lists = gridViewColumnVisibilitys.Where(x => x.IsVisible == true).ToList();

            for (int i = 0; i < lists.Count; i++)
            {
                var desiredColumn = lists[i].GridViewColumn;
                if (gridViewColumns.Contains(desiredColumn))
                {
                    var actualIndex = gridViewColumns.IndexOf(desiredColumn);
                    // 如果当前列的位置不正确，则将其移动到正确的位置
                    if (actualIndex != i)
                    {
                        gridViewColumns.Move(actualIndex, i);
                    }
                }
                else
                {
                    gridViewColumns.Insert(i, desiredColumn);
                }
            }
        }

        public static void AdjustGridViewColumnAuto(GridViewColumnCollection gridViewColumns, ObservableCollection<GridViewColumnVisibility> gridViewColumnVisibilitys)
        {
            ///HashSet效率更高
            var invisibleColumns = new HashSet<GridViewColumn>(gridViewColumnVisibilitys.Where(x => !x.IsVisible).Select(x => x.GridViewColumn));

            gridViewColumns.RemoveAll(column => invisibleColumns.Contains(column));
            var lists = gridViewColumnVisibilitys.Where(x => x.IsVisible == true).ToList();

            for (int i = 0; i < lists.Count; i++)
            {
                var desiredColumn = lists[i].GridViewColumn;
                desiredColumn.Width = double.NaN;
            }
        }

        public static ObservableCollection<GridViewColumnVisibility> GenContentMenuGridViewColumnZero(ContextMenu contextMenu, GridViewColumnCollection gridViewColumns, ObservableCollection<GridViewColumnVisibility>? gridViewColumnVisibilitys = null, ListView? associatedListView = null)
        {
            if (gridViewColumnVisibilitys == null)
            {
                gridViewColumnVisibilitys = new ObservableCollection<GridViewColumnVisibility>();
            }
            else
            {
                gridViewColumnVisibilitys.Clear();
            }
            AddGridViewColumn(gridViewColumns, gridViewColumnVisibilitys);
            contextMenu.Items.Clear();
            GenContentMenuGridViewColumn(contextMenu, gridViewColumns, gridViewColumnVisibilitys, associatedListView);
            return gridViewColumnVisibilitys;
        }

        public static void GenContentMenuGridViewColumn(ContextMenu contextMenu, GridViewColumnCollection gridViewColumns, ObservableCollection<GridViewColumnVisibility> gridViewColumnVisibilitys, ListView? associatedListView = null)
        {
            MenuItem menuItemAuto = new();
            menuItemAuto.Header = Properties.Resources.AutoAdjustColumnWidth;
            menuItemAuto.Click += (s, e) =>
            {
                AdjustGridViewColumnAuto(gridViewColumns, gridViewColumnVisibilitys);
            };
            contextMenu.Items.Add(menuItemAuto);

            // 添加通用排序选项
            if (associatedListView != null)
            {
                MenuItem sortMenu = new() { Header = "排序选项" };
                
                // 智能排序
                MenuItem smartSortAsc = new() { Header = "智能排序 (升序)" };
                smartSortAsc.Click += (s, e) => gridViewColumnVisibilitys.SmartSort(associatedListView, false);
                sortMenu.Items.Add(smartSortAsc);
                
                MenuItem smartSortDesc = new() { Header = "智能排序 (降序)" };
                smartSortDesc.Click += (s, e) => gridViewColumnVisibilitys.SmartSort(associatedListView, true);
                sortMenu.Items.Add(smartSortDesc);
                
                sortMenu.Items.Add(new Separator());
                
                // 按列排序
                foreach (var column in gridViewColumnVisibilitys.Where(x => x.IsVisible))
                {
                    var columnName = column.ColumnName?.ToString();
                    if (string.IsNullOrEmpty(columnName)) continue;
                    
                    MenuItem columnSortMenu = new() { Header = $"按 {columnName} 排序" };
                    
                    MenuItem sortAsc = new() { Header = "升序" };
                    sortAsc.Click += (s, e) => gridViewColumnVisibilitys.SortListViewData(associatedListView, columnName, false);
                    
                    MenuItem sortDesc = new() { Header = "降序" };
                    sortDesc.Click += (s, e) => gridViewColumnVisibilitys.SortListViewData(associatedListView, columnName, true);
                    
                    columnSortMenu.Items.Add(sortAsc);
                    columnSortMenu.Items.Add(sortDesc);
                    sortMenu.Items.Add(columnSortMenu);
                }
                
                contextMenu.Items.Add(sortMenu);
            }

            contextMenu.Items.Add(new Separator());
            
            foreach (var item in gridViewColumnVisibilitys)
            {
                MenuItem menuItem = new MenuItem();
                menuItem.Header = item.ColumnName;
                Binding binding = new Binding("IsVisible")
                {
                    Source = item,
                    Mode = BindingMode.TwoWay // 双向绑定
                };
                menuItem.SetBinding(MenuItem.IsCheckedProperty, binding);
                menuItem.Click += (s, e) =>
                {
                    item.IsVisible = !item.IsVisible;
                };
                item.VisibleChanged += (s, e) =>
                {
                    AdjustGridViewColumn(gridViewColumns, gridViewColumnVisibilitys);
                };
                contextMenu.Items.Add(menuItem);
            }
        }

        public object ColumnName { get; set; }

        [JsonIgnore]
        public GridViewColumn GridViewColumn { get; set; }

        public event EventHandler VisibleChanged;

        public bool IsVisible { get => _IsVisible; set { _IsVisible = value; OnPropertyChanged(); VisibleChanged?.Invoke(this, new EventArgs()); } }
        private bool _IsVisible;

        public bool IsSortD { get => _IsSortD; set { _IsSortD = value; OnPropertyChanged(); } }
        private bool _IsSortD;
    }
}
