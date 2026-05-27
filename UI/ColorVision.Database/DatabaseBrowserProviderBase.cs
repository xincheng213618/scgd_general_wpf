using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using ColorVision.Database.Properties;

namespace ColorVision.Database
{
    public abstract class DatabaseBrowserProviderBase : IDatabaseBrowserProvider
    {
        public abstract string ProviderId { get; }
        public abstract string ProviderName { get; }
        public abstract DatabaseType DatabaseType { get; }
        public virtual bool CanWrite => true;

        public abstract IReadOnlyList<DatabaseCatalogInfo> GetDatabases();
        public abstract IReadOnlyList<DatabaseTableInfo> GetTables(string databaseName);
        public abstract IReadOnlyList<DatabaseColumnInfo> GetColumns(DatabaseTableInfo table);
        public abstract DatabaseTablePage QueryPage(DatabaseTableInfo table, int pageIndex, int pageSize, string? keyword, string? sortColumn, ListSortDirection sortDirection);
        public abstract int InsertRow(DatabaseTableInfo table, IReadOnlyDictionary<string, object?> values);
        public abstract int UpdateRow(DatabaseTableInfo table, IReadOnlyDictionary<string, object?> keys, IReadOnlyDictionary<string, object?> values);
        public abstract int DeleteRow(DatabaseTableInfo table, IReadOnlyDictionary<string, object?> keys);

        protected abstract string QuoteIdentifier(string identifier);
        protected abstract string GetTableSql(DatabaseTableInfo table);

        protected static SugarParameter Parameter(string name, object? value)
        {
            return new SugarParameter(name, value ?? DBNull.Value);
        }

        protected static int ReadInt(DataTable table)
        {
            if (table.Rows.Count == 0 || table.Columns.Count == 0)
                return 0;

            return Convert.ToInt32(table.Rows[0][0]);
        }

        protected static string ReadString(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
                return string.Empty;

            return Convert.ToString(row[columnName]) ?? string.Empty;
        }

        protected static int ReadInt(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
                return 0;

            return Convert.ToInt32(row[columnName]);
        }

        protected static long? ReadNullableLong(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
                return null;

            return Convert.ToInt64(row[columnName]);
        }

        protected static bool ContainsText(string storeType)
        {
            if (string.IsNullOrWhiteSpace(storeType)) return false;

            var normalized = storeType.ToUpperInvariant();
            return normalized.Contains("CHAR")
                   || normalized.Contains("TEXT")
                   || normalized.Contains("CLOB")
                   || normalized.Contains("JSON")
                   || normalized.Contains("ENUM")
                   || normalized.Contains("SET");
        }

        protected string BuildWhereClause(
            IReadOnlyList<DatabaseColumnInfo> columns,
            string? keyword,
            List<SugarParameter> parameters,
            bool castTextColumns)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return string.Empty;

            var textColumns = columns.Where(column => column.IsTextLike).ToList();
            if (textColumns.Count == 0)
                return string.Empty;

            parameters.Add(Parameter("@keyword", $"%{keyword.Trim()}%"));
            var conditions = textColumns.Select(column =>
            {
                var columnSql = QuoteIdentifier(column.ColumnName);
                return castTextColumns
                    ? $"CAST({columnSql} AS TEXT) LIKE @keyword"
                    : $"{columnSql} LIKE @keyword";
            });

            return " WHERE " + string.Join(" OR ", conditions);
        }

        protected string BuildOrderClause(IReadOnlyList<DatabaseColumnInfo> columns, string? sortColumn, ListSortDirection sortDirection)
        {
            var column = columns.FirstOrDefault(item => string.Equals(item.ColumnName, sortColumn, StringComparison.OrdinalIgnoreCase))
                         ?? columns.FirstOrDefault(item => item.IsPrimaryKey);

            if (column == null && columns.Count > 0)
                column = columns[0];

            if (column == null)
                return string.Empty;

            var direction = sortDirection == ListSortDirection.Ascending ? "ASC" : "DESC";
            return $" ORDER BY {QuoteIdentifier(column.ColumnName)} {direction}";
        }

        protected string BuildInsertSql(DatabaseTableInfo table, IReadOnlyList<DatabaseColumnInfo> columns, IReadOnlyDictionary<string, object?> values, List<SugarParameter> parameters)
        {
            var writableColumns = columns
                .Where(column => !column.IsIdentity && values.ContainsKey(column.ColumnName))
                .ToList();

            if (writableColumns.Count == 0)
                throw new InvalidOperationException(Properties.Resources.DB_NoInsertableColumn);

            var columnNames = new List<string>();
            var parameterNames = new List<string>();
            for (var index = 0; index < writableColumns.Count; index++)
            {
                var column = writableColumns[index];
                var parameterName = $"@p{index}";
                columnNames.Add(QuoteIdentifier(column.ColumnName));
                parameterNames.Add(parameterName);
                parameters.Add(Parameter(parameterName, values[column.ColumnName]));
            }

            return $"INSERT INTO {GetTableSql(table)} ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", parameterNames)})";
        }

        protected string BuildUpdateSql(DatabaseTableInfo table, IReadOnlyList<DatabaseColumnInfo> columns, IReadOnlyDictionary<string, object?> keys, IReadOnlyDictionary<string, object?> values, List<SugarParameter> parameters)
        {
            if (keys.Count == 0)
                throw new InvalidOperationException("没有主键条件，不能更新。");

            var writableColumns = columns
                .Where(column => !column.IsIdentity && !column.IsPrimaryKey && values.ContainsKey(column.ColumnName))
                .ToList();

            if (writableColumns.Count == 0)
                throw new InvalidOperationException("没有可更新的列。");

            var setParts = new List<string>();
            for (var index = 0; index < writableColumns.Count; index++)
            {
                var column = writableColumns[index];
                var parameterName = $"@p{index}";
                setParts.Add($"{QuoteIdentifier(column.ColumnName)} = {parameterName}");
                parameters.Add(Parameter(parameterName, values[column.ColumnName]));
            }

            var where = BuildKeyWhere(keys, parameters, writableColumns.Count);
            return $"UPDATE {GetTableSql(table)} SET {string.Join(", ", setParts)} WHERE {where}";
        }

        protected string BuildDeleteSql(DatabaseTableInfo table, IReadOnlyDictionary<string, object?> keys, List<SugarParameter> parameters)
        {
            if (keys.Count == 0)
                throw new InvalidOperationException("没有主键条件，不能删除。");

            return $"DELETE FROM {GetTableSql(table)} WHERE {BuildKeyWhere(keys, parameters, 0)}";
        }

        private string BuildKeyWhere(IReadOnlyDictionary<string, object?> keys, List<SugarParameter> parameters, int startIndex)
        {
            var builder = new StringBuilder();
            var index = startIndex;

            foreach (var key in keys)
            {
                if (builder.Length > 0)
                    builder.Append(" AND ");

                var parameterName = $"@p{index}";
                builder.Append(QuoteIdentifier(key.Key)).Append(" = ").Append(parameterName);
                parameters.Add(Parameter(parameterName, key.Value));
                index++;
            }

            return builder.ToString();
        }
    }
}
