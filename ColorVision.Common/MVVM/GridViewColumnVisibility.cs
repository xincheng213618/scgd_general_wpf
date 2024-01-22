using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Linq;
using ColorVision.Common.Extension;

namespace ColorVision.Common.MVVM
{
    public class GridViewColumnVisibility
    {
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

        public static void AddGridViewColumn(GridViewColumnCollection gridViewColumns, ObservableCollection<GridViewColumnVisibility> gridViewColumnVisibilitys)
        {
            gridViewColumnVisibilitys ??= new ObservableCollection<GridViewColumnVisibility>();
            foreach (var item in gridViewColumns)
                gridViewColumnVisibilitys.Add(new GridViewColumnVisibility() { ColumnName = item.Header, GridViewColumn = item, IsVisible = true });
        }

        public object ColumnName { get; set; }    

        public GridViewColumn GridViewColumn { get; set; }

        public bool IsVisible { get; set; }
    }
}
