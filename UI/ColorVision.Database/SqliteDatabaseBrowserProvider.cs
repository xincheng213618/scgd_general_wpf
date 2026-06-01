using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using ColorVision.Database.Properties;

namespace ColorVision.Database
{
    public sealed class SqliteDatabaseBrowserProvider : DatabaseBrowserProviderBase
    {
        private readonly Func<string> _dbPathFactory;
        private readonly Func<string, SqlSugarClient> _clientFactory;
        private readonly string _providerId;
        private readonly string _providerName;

        public SqliteDatabaseBrowserProvider(string providerId, string providerName, Func<string> dbPathFactory, Func<string, SqlSugarClient> clientFactory)
        {
            _providerId = string.IsNullOrWhiteSpace(providerId) ? throw new ArgumentException(Properties.Resources.DB_ProviderIdEmpty, nameof(providerId)) : providerId;
            _providerName = string.IsNullOrWhiteSpace(providerName) ? "SQLite" : providerName;
            _dbPathFactory = dbPathFactory ?? throw new ArgumentNullException(nameof(dbPathFactory));
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        }

        public override string ProviderId => _providerId;
        public override string ProviderName => _providerName;
        public override DatabaseType DatabaseType => DatabaseType.Sqlite;

        public override IReadOnlyList<DatabaseCatalogInfo> GetDatabases()
        {
            var dbPath = GetDbPath();
            var displayName = Path.GetFileName(dbPath);
            return new List<DatabaseCatalogInfo>
            {
                new()
                {
                    ProviderId = ProviderId,
                    ProviderName = ProviderName,
                    DatabaseType = DatabaseType,
                    Name = "main",
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? "main" : displayName,
                    SourceDetail = dbPath,
                    CanWrite = CanWrite
                }
            };
        }

        public override IReadOnlyList<DatabaseTableInfo> GetTables(string databaseName)
        {
            using var db = CreateClient();
            var sql = @"SELECT name AS TableName
FROM sqlite_master
WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
ORDER BY name";

            var table = db.Ado.GetDataTable(sql);
            return table.Rows.Cast<DataRow>()
                .Select(row =>
                {
                    var tableName = ReadString(row, "TableName");
                    return new DatabaseTableInfo
                    {
                        ProviderId = ProviderId,
                        ProviderName = ProviderName,
                        DatabaseType = DatabaseType,
                        DatabaseName = string.IsNullOrWhiteSpace(databaseName) ? "main" : databaseName,
                        TableName = tableName,
                        DisplayName = tableName,
                        RowCount = TryGetTableCount(db, tableName),
                        CanWrite = CanWrite
                    };
                })
                .ToList();
        }

        public override IReadOnlyList<DatabaseColumnInfo> GetColumns(DatabaseTableInfo table)
        {
            using var db = CreateClient();
            var result = db.Ado.GetDataTable($"PRAGMA table_info({QuoteIdentifier(table.TableName)})");
            return result.Rows.Cast<DataRow>()
                .Select(row =>
                {
                    var storeType = ReadString(row, "type");
                    var pk = ReadInt(row, "pk");
                    return new DatabaseColumnInfo
                    {
                        ColumnName = ReadString(row, "name"),
                        DisplayName = ReadString(row, "name"),
                        StoreType = storeType,
                        Ordinal = ReadInt(row, "cid") + 1,
                        IsNullable = ReadInt(row, "notnull") == 0,
                        IsPrimaryKey = pk > 0,
                        IsIdentity = pk > 0 && storeType.Contains("INT", StringComparison.OrdinalIgnoreCase),
                        IsReadOnly = false,
                        IsTextLike = ContainsText(storeType) || string.IsNullOrWhiteSpace(storeType),
                        DefaultValue = row.Table.Columns.Contains("dflt_value") ? row["dflt_value"] : null
                    };
                })
                .ToList();
        }

        public override DatabaseTablePage QueryPage(DatabaseTableInfo table, int pageIndex, int pageSize, string? keyword, string? sortColumn, ListSortDirection sortDirection)
        {
            using var db = CreateClient();
            var columns = GetColumns(table);
            var parameters = new List<SugarParameter>();
            var whereSql = BuildWhereClause(columns, keyword, parameters, true);
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
            using var db = CreateClient();
            var parameters = new List<SugarParameter>();
            var sql = BuildInsertSql(table, GetColumns(table), values, parameters);
            return db.Ado.ExecuteCommand(sql, parameters.ToArray());
        }

        public override int UpdateRow(DatabaseTableInfo table, IReadOnlyDictionary<string, object?> keys, IReadOnlyDictionary<string, object?> values)
        {
            using var db = CreateClient();
            var parameters = new List<SugarParameter>();
            var sql = BuildUpdateSql(table, GetColumns(table), keys, values, parameters);
            return db.Ado.ExecuteCommand(sql, parameters.ToArray());
        }

        public override int DeleteRow(DatabaseTableInfo table, IReadOnlyDictionary<string, object?> keys)
        {
            using var db = CreateClient();
            var parameters = new List<SugarParameter>();
            var sql = BuildDeleteSql(table, keys, parameters);
            return db.Ado.ExecuteCommand(sql, parameters.ToArray());
        }

        private long? TryGetTableCount(SqlSugarClient db, string tableName)
        {
            try
            {
                return ReadInt(db.Ado.GetDataTable($"SELECT COUNT(*) FROM {QuoteIdentifier(tableName)}"));
            }
            catch
            {
                return null;
            }
        }

        private SqlSugarClient CreateClient()
        {
            return _clientFactory(GetDbPath());
        }

        private string GetDbPath()
        {
            return Environment.ExpandEnvironmentVariables(_dbPathFactory() ?? string.Empty);
        }

        protected override string QuoteIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                throw new ArgumentException(Properties.Resources.DB_DbIdEmpty, nameof(identifier));

            return $"\"{identifier.Replace("\"", "\"\"")}\"";
        }

        protected override string GetTableSql(DatabaseTableInfo table)
        {
            return QuoteIdentifier(table.TableName);
        }
    }
}
