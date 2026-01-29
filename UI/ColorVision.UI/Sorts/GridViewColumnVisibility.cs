using ColorVision.Common.Utilities;
using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
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

        public static ObservableCollection<GridViewColumnVisibility> GenContentMenuGridViewColumnZero(ContextMenu contextMenu, GridViewColumnCollection gridViewColumns, ObservableCollection<GridViewColumnVisibility>? gridViewColumnVisibilitys = null)
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
            GenContentMenuGridViewColumn(contextMenu, gridViewColumns, gridViewColumnVisibilitys);
            return gridViewColumnVisibilitys;
        }

        public static void GenContentMenuGridViewColumn(ContextMenu contextMenu, GridViewColumnCollection gridViewColumns, ObservableCollection<GridViewColumnVisibility> gridViewColumnVisibilitys)
        {
            MenuItem menuItemAuto = new();
            menuItemAuto.Header = Properties.Resources.AutoAdjustColumnWidth;
            menuItemAuto.Click += (s, e) =>
            {
                AdjustGridViewColumnAuto(gridViewColumns, gridViewColumnVisibilitys);
            };
            contextMenu.Items.Add(menuItemAuto);
            
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
