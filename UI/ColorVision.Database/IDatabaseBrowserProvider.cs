using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Database
{
    public interface IDatabaseBrowserProvider
    {
        string ProviderId { get; }
        string ProviderName { get; }
        DatabaseType DatabaseType { get; }
        bool CanWrite { get; }

        IReadOnlyList<DatabaseCatalogInfo> GetDatabases();
        IReadOnlyList<DatabaseTableInfo> GetTables(string databaseName);
        IReadOnlyList<DatabaseColumnInfo> GetColumns(DatabaseTableInfo table);
        DatabaseTablePage QueryPage(DatabaseTableInfo table, int pageIndex, int pageSize, string? keyword, string? sortColumn, ListSortDirection sortDirection);
        int InsertRow(DatabaseTableInfo table, IReadOnlyDictionary<string, object?> values);
        int UpdateRow(DatabaseTableInfo table, IReadOnlyDictionary<string, object?> keys, IReadOnlyDictionary<string, object?> values);
        int DeleteRow(DatabaseTableInfo table, IReadOnlyDictionary<string, object?> keys);
    }
}
