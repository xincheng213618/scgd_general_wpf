using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using ColorVision.Database.Properties;

namespace ColorVision.Database
{
    public sealed class MySqlDatabaseBrowserProvider : DatabaseBrowserProviderBase
    {
        private readonly Func<MySqlConfig> _configFactory;

        public MySqlDatabaseBrowserProvider(Func<MySqlConfig> configFactory)
        {
            _configFactory = configFactory ?? throw new ArgumentNullException(nameof(configFactory));
        }

        public override string ProviderId => "mysql.default";
        public override string ProviderName => "MySQL";
        public override DatabaseType DatabaseType => DatabaseType.MySql;

        public override IReadOnlyList<DatabaseCatalogInfo> GetDatabases()
        {
            using var db = CreateClient(null);
            var sql = @"SELECT SCHEMA_NAME AS Name
FROM INFORMATION_SCHEMA.SCHEMATA
WHERE SCHEMA_NAME NOT IN ('information_schema', 'mysql', 'performance_schema', 'sys')
ORDER BY SCHEMA_NAME";

            var table = db.Ado.GetDataTable(sql);
            return table.Rows.Cast<DataRow>()
                .Select(row =>
                {
                    var name = ReadString(row, "Name");
                    return new DatabaseCatalogInfo
                    {
                        ProviderId = ProviderId,
                        ProviderName = ProviderName,
                        DatabaseType = DatabaseType,
                        Name = name,
                        DisplayName = name,
                        SourceDetail = $"{_configFactory().Host}:{_configFactory().Port}/{name}",
                        CanWrite = CanWrite
                    };
                })
                .ToList();
        }

        public override IReadOnlyList<DatabaseTableInfo> GetTables(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
                databaseName = _configFactory().Database;

            using var db = CreateClient(databaseName);
            var sql = @"SELECT TABLE_NAME AS TableName,
       TABLE_COMMENT AS Comment,
       ENGINE AS Engine,
       TABLE_ROWS AS RowCount
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = @databaseName AND TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_NAME";

            var table = db.Ado.GetDataTable(sql, Parameter("@databaseName", databaseName));
            return table.Rows.Cast<DataRow>()
                .Select(row =>
                {
                    var tableName = ReadString(row, "TableName");
                    return new DatabaseTableInfo
                    {
                        ProviderId = ProviderId,
                        ProviderName = ProviderName,
                        DatabaseType = DatabaseType,
                        DatabaseName = databaseName,
                        TableName = tableName,
                        DisplayName = tableName,
                        Comment = ReadString(row, "Comment"),
                        Engine = ReadString(row, "Engine"),
                        RowCount = ReadNullableLong(row, "RowCount"),
                        CanWrite = CanWrite
                    };
                })
                .ToList();
        }

        public override IReadOnlyList<DatabaseColumnInfo> GetColumns(DatabaseTableInfo table)
        {
            using var db = CreateClient(table.DatabaseName);
            var sql = @"SELECT COLUMN_NAME AS ColumnName,
       DATA_TYPE AS DataType,
       COLUMN_TYPE AS ColumnType,
       IS_NULLABLE AS IsNullable,
       COLUMN_KEY AS ColumnKey,
       EXTRA AS Extra,
       COLUMN_DEFAULT AS DefaultValue,
       COLUMN_COMMENT AS Comment,
       ORDINAL_POSITION AS Ordinal
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = @databaseName AND TABLE_NAME = @tableName
ORDER BY ORDINAL_POSITION";

            var result = db.Ado.GetDataTable(sql, Parameter("@databaseName", table.DatabaseName), Parameter("@tableName", table.TableName));
            return result.Rows.Cast<DataRow>()
                .Select(row =>
                {
                    var columnType = ReadString(row, "ColumnType");
                    var dataType = ReadString(row, "DataType");
                    var extra = ReadString(row, "Extra");
                    return new DatabaseColumnInfo
                    {
                        ColumnName = ReadString(row, "ColumnName"),
                        DisplayName = ReadString(row, "ColumnName"),
                        StoreType = string.IsNullOrWhiteSpace(columnType) ? dataType : columnType,
                        Comment = ReadString(row, "Comment"),
                        Ordinal = ReadInt(row, "Ordinal"),
                        IsNullable = string.Equals(ReadString(row, "IsNullable"), "YES", StringComparison.OrdinalIgnoreCase),
                        IsPrimaryKey = string.Equals(ReadString(row, "ColumnKey"), "PRI", StringComparison.OrdinalIgnoreCase),
                        IsIdentity = extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase),
                        IsReadOnly = extra.Contains("VIRTUAL", StringComparison.OrdinalIgnoreCase) || extra.Contains("STORED", StringComparison.OrdinalIgnoreCase),
                        IsTextLike = ContainsText(dataType),
                        DefaultValue = row.Table.Columns.Contains("DefaultValue") ? row["DefaultValue"] : null
                    };
                })
                .ToList();
        }

        public override DatabaseTablePage QueryPage(DatabaseTableInfo table, int pageIndex, int pageSize, string? keyword, string? sortColumn, ListSortDirection sortDirection)
        {
            using var db = CreateClient(table.DatabaseName);
            var columns = GetColumns(table);
            var parameters = new List<SugarParameter>();
            var whereSql = BuildWhereClause(columns, keyword, parameters, false);
            var orderSql = BuildOrderClause(columns, sortColumn, sortDirection);
            var offset = Math.Max(0, (Math.Max(1, pageIndex) - 1) * Math.Max(1, pageSize));
            var limit = Math.Max(1, pageSize);

            parameters.Add(Parameter("@limit", limit));
            parameters.Add(Parameter("@offset", offset));

            var countSql = $"SELECT COUNT(*) FROM {GetTableSql(table)}{whereSql}";
            var pageSql = $"SELECT * FROM {GetTableSql(table)}{whereSql}{orderSql} LIMIT @limit OFFSET @offset";

            var total = ReadInt(db.Ado.GetDataTable(countSql, parameters.Where(item => item.ParameterName != "@limit" && item.ParameterName != "@offset").ToArray()));
            var rows = db.Ado.GetDataTable(pageSql, parameters.ToArray());
            return new DatabaseTablePage(rows, total);
        }

        public override int InsertRow(DatabaseTableInfo table, IReadOnlyDictionary<string, object?> values)
        {
            using var db = CreateClient(table.DatabaseName);
            var parameters = new List<SugarParameter>();
            var sql = BuildInsertSql(table, GetColumns(table), values, parameters);
            return db.Ado.ExecuteCommand(sql, parameters.ToArray());
        }

        public override int UpdateRow(DatabaseTableInfo table, IReadOnlyDictionary<string, object?> keys, IReadOnlyDictionary<string, object?> values)
        {
            using var db = CreateClient(table.DatabaseName);
            var parameters = new List<SugarParameter>();
            var sql = BuildUpdateSql(table, GetColumns(table), keys, values, parameters);
            return db.Ado.ExecuteCommand(sql, parameters.ToArray());
        }

        public override int DeleteRow(DatabaseTableInfo table, IReadOnlyDictionary<string, object?> keys)
        {
            using var db = CreateClient(table.DatabaseName);
            var parameters = new List<SugarParameter>();
            var sql = BuildDeleteSql(table, keys, parameters);
            return db.Ado.ExecuteCommand(sql, parameters.ToArray());
        }

        internal SqlSugarClient CreateClient(string? databaseName)
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(_configFactory(), 2, databaseName),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
        }

        protected override string QuoteIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                throw new ArgumentException(Properties.Resources.DB_DbIdEmpty, nameof(identifier));

            return $"`{identifier.Replace("`", "``")}`";
        }

        protected override string GetTableSql(DatabaseTableInfo table)
        {
            return $"{QuoteIdentifier(table.DatabaseName)}.{QuoteIdentifier(table.TableName)}";
        }
    }
}
