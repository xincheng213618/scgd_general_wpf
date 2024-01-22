using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Linq;
using ColorVision.Common.Extension;
using ColorVision.MVVM;

namespace ColorVision.Common.MVVM
{
    public static class GridViewColumnVisibilityExtension
    {
        public static void AddGridViewColumn(this ObservableCollection<GridViewColumnVisibility> gridViewColumnVisibilitys, GridViewColumnCollection gridViewColumns) => GridViewColumnVisibility.AddGridViewColumn(gridViewColumns, gridViewColumnVisibilitys);
        public static void AdjustGridViewColumn(this ObservableCollection<GridViewColumnVisibility> gridViewColumnVisibilitys, GridViewColumnCollection gridViewColumns) => GridViewColumnVisibility.AdjustGridViewColumn(gridViewColumns, gridViewColumnVisibilitys);
    }

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

        public object ColumnName { get; set; }    

        public GridViewColumn GridViewColumn { get; set; }

        public bool IsVisible { get => _IsVisible; set { _IsVisible = value; NotifyPropertyChanged(); } }
        private bool _IsVisible;
    }
}
