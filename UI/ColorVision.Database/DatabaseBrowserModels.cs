using System.Collections.Generic;
using System.Data;

namespace ColorVision.Database
{
    public sealed class DatabaseCatalogInfo
    {
        public string ProviderId { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public DatabaseType DatabaseType { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SourceDetail { get; set; } = string.Empty;
        public bool CanWrite { get; set; } = true;

        public override string ToString() => string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName;
    }

    public sealed class DatabaseTableInfo
    {
        public string ProviderId { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public DatabaseType DatabaseType { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string Engine { get; set; } = string.Empty;
        public long? RowCount { get; set; }
        public bool CanWrite { get; set; } = true;

        public override string ToString() => TableName;
    }

    public sealed class DatabaseColumnInfo
    {
        public string ColumnName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string StoreType { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public int Ordinal { get; set; }
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsReadOnly { get; set; }
        public bool IsTextLike { get; set; }
        public object? DefaultValue { get; set; }
    }

    public sealed class DatabaseTablePage
    {
        public DatabaseTablePage(DataTable rows, int totalCount)
        {
            Rows = rows;
            TotalCount = totalCount;
        }

        public DataTable Rows { get; }
        public int TotalCount { get; }
    }

    public sealed class DatabaseRowChange
    {
        public Dictionary<string, object?> Values { get; } = new();
        public Dictionary<string, object?> Keys { get; } = new();
    }
}
