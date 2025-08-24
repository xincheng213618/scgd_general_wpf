#pragma warning disable IDE0060
using System;
using System.Data;
using System.Linq;

namespace ColorVision.Database
{
    public static class DataTableExtension
    {
        //如果找到两个或多个行，则返回第一个行
        public static DataRow? SelectRow(this DataTable dataTable, int id)
        {
            ArgumentNullException.ThrowIfNull(dataTable);
            if (!dataTable.Columns.Contains("id"))
                throw new ArgumentException("Column 'id' does not exist in the DataTable.");
            var rows = dataTable.AsEnumerable().Where(row => row.Field<int>("id") == id).ToList();
            return rows.Count == 1 ? rows[0] : null;
        }

        public static object? IsDBNull<T>(this DataRow dataTable, T t) => IsDBNull(t);

        public static object? IsDBNull<T> (T t)
        {
            return t != null ? t : DBNull.Value;
        }

        public static DataRow GetRow<T>(this DataTable dataTable, T item) where T : IPKModel
        {
            ArgumentNullException.ThrowIfNull(dataTable);
            ArgumentNullException.ThrowIfNull(item);
            DataRow row = dataTable.SelectRow(item.Id);
            if (row == null)
            {
                row = dataTable.NewRow();
                dataTable.Rows.Add(row);
            }
            return row;
        }
    }
}
