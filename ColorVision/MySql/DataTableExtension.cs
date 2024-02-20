using System;
using System.Data;
using System.Linq;

namespace ColorVision.MySql
{
    public static class DataTableExtension
    {
        public static DataRow? SelectRow(this DataTable dataTable, int id)
        {
            if (dataTable == null)
                throw new ArgumentNullException(nameof(dataTable));

            if (!dataTable.Columns.Contains("id"))
                throw new ArgumentException("Column 'id' does not exist in the DataTable.");
            var rows = dataTable.AsEnumerable().Where(row => row.Field<int>("id") == id).ToList();
            return rows.Count == 1 ? rows[0] : null;
        }

        public static DataRow GetRow<T>(this DataTable dataTable, T item) where T : IPKModel
        {
            DataRow row = dataTable.SelectRow(item.GetPK());
            if (row == null)
            {
                row = dataTable.NewRow();
                dataTable.Rows.Add(row);
            }
            return row;
        }
    }
}
