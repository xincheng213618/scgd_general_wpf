using ColorVision.Common.Extension;
using ColorVision.MVVM;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Common.MVVM
{
    public static class GridViewColumnVisibilityExtension
    {
        public static void AddGridViewColumn(this ObservableCollection<GridViewColumnVisibility> gridViewColumnVisibilitys, GridViewColumnCollection gridViewColumns) => GridViewColumnVisibility.AddGridViewColumn(gridViewColumns, gridViewColumnVisibilitys);
        public static void AdjustGridViewColumn(this ObservableCollection<GridViewColumnVisibility> gridViewColumnVisibilitys, GridViewColumnCollection gridViewColumns) => GridViewColumnVisibility.AdjustGridViewColumn(gridViewColumns, gridViewColumnVisibilitys);

        public static void AdjustGridViewColumnAuto(this ObservableCollection<GridViewColumnVisibility> gridViewColumnVisibilitys, GridViewColumnCollection gridViewColumns) => GridViewColumnVisibility.AdjustGridViewColumnAuto(gridViewColumns, gridViewColumnVisibilitys);
    }

   // https://stackoverflow.com/questions/747872/wpf-displaying-a-context-menu-for-a-gridviews-items
    public class GridViewColumnVisibility:ViewModelBase
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

        public static void GenContentMenuGridViewColumn(ContextMenu contextMenu,GridViewColumnCollection gridViewColumns, ObservableCollection<GridViewColumnVisibility> gridViewColumnVisibilitys)
        {
            MenuItem menuItemAuto = new MenuItem();
            menuItemAuto.Header = "自动调整列宽";
            menuItemAuto.Click += (s, e) =>
            {
                AdjustGridViewColumnAuto(gridViewColumns, gridViewColumnVisibilitys);
            };
            contextMenu.Items.Add(menuItemAuto);
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
                    AdjustGridViewColumn(gridViewColumns, gridViewColumnVisibilitys);
                };
                contextMenu.Items.Add(menuItem);
            }
        }




        public object ColumnName { get; set; }    

        public GridViewColumn GridViewColumn { get; set; }

        public bool IsVisible { get => _IsVisible; set { _IsVisible = value; NotifyPropertyChanged(); } }
        private bool _IsVisible;

        public bool IsSort { get => _IsSort; set { _IsSort = value; NotifyPropertyChanged(); } }
        private bool _IsSort;

        public bool IsSortD { get => _IsSortD; set { _IsSortD = value; NotifyPropertyChanged(); } }
        private bool _IsSortD;
    }
}
