using ColorVision.Database;
using SqlSugar;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace ColorVision.UI.Tests;

public sealed class GenericQueryConditionSupportTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"colorvision-generic-query-{Guid.NewGuid():N}");
    private readonly List<SqlSugarClient> _clients = [];

    [Fact]
    public void QueryableProperties_UseFriendlyNamesAndExcludeUnsupportedMembers()
    {
        IReadOnlyList<KeyValuePair<string, PropertyInfo>> properties =
            GenericQueryConditionSupport.GetQueryableProperties(typeof(QueryEntity));

        Assert.Contains(properties, item => item.Key == "层级" && item.Value.Name == nameof(QueryEntity.ZIndex));
        Assert.DoesNotContain(properties, item => item.Key == "z_index");
        Assert.DoesNotContain(properties, item => item.Value.Name == nameof(QueryEntity.Ignored));
        Assert.DoesNotContain(properties, item => item.Value.Name == nameof(QueryEntity.Hidden));
    }

    [Fact]
    public void ConditionValue_DistinguishesMissingInvalidAndZero()
    {
        PropertyInfo property = typeof(QueryEntity).GetProperty(nameof(QueryEntity.ZIndex))!;
        var condition = new QueryCondition { Property = property };

        Assert.False(GenericQueryConditionSupport.TryGetConditionValue(condition, out _, out string missingError));
        Assert.NotEmpty(missingError);

        condition.InputText = "not-a-number";
        Assert.False(GenericQueryConditionSupport.TryGetConditionValue(condition, out _, out string invalidError));
        Assert.NotEmpty(invalidError);

        condition.InputText = "0";
        Assert.True(GenericQueryConditionSupport.TryGetConditionValue(condition, out object? value, out string error));
        Assert.Equal(0, value);
        Assert.Empty(error);
    }

    [Fact]
    public void ApplyConditions_SupportsBooleanAndDuplicateRangeFields()
    {
        SqlSugarClient db = CreateDatabase();
        db.Insertable(new List<QueryEntity>
        {
            new QueryEntity { Id = 1, ZIndex = 5, Enabled = false, Name = "outside" },
            new QueryEntity { Id = 2, ZIndex = 15, Enabled = false, Name = "match" },
            new QueryEntity { Id = 3, ZIndex = 15, Enabled = true, Name = "wrong flag" },
            new QueryEntity { Id = 4, ZIndex = 25, Enabled = false, Name = "outside" }
        }).ExecuteCommand();

        PropertyInfo zIndex = typeof(QueryEntity).GetProperty(nameof(QueryEntity.ZIndex))!;
        PropertyInfo enabled = typeof(QueryEntity).GetProperty(nameof(QueryEntity.Enabled))!;
        QueryCondition[] conditions =
        [
            new() { Property = zIndex, Operator = QueryOperator.GreaterOrEqual, InputText = "10" },
            new() { Property = zIndex, Operator = QueryOperator.LessOrEqual, InputText = "20" },
            new() { Property = enabled, Operator = QueryOperator.Equal, Value = false }
        ];

        List<QueryEntity> results = GenericQueryConditionSupport
            .ApplyConditions(db.Queryable<QueryEntity>(), conditions)
            .ToList();

        QueryEntity result = Assert.Single(results);
        Assert.Equal(2, result.Id);
    }

    [Fact]
    public void InvalidCondition_DoesNotClearExistingResults()
    {
        RunOnSta(() =>
        {
            SqlSugarClient db = CreateDatabase();
            var existing = new QueryEntity { Id = 99, Name = "keep" };
            var results = new List<QueryEntity> { existing };
            var query = new GenericQuery<QueryEntity>(db, results);
            query.QueryConditions.Add(new QueryCondition
            {
                Property = typeof(QueryEntity).GetProperty(nameof(QueryEntity.ZIndex))!,
                Operator = QueryOperator.Equal,
                InputText = "invalid"
            });

            Assert.Throws<FormatException>(query.QueryDB);
            Assert.Same(existing, Assert.Single(results));
        });
    }

    private SqlSugarClient CreateDatabase()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        string databasePath = Path.Combine(_temporaryDirectory, $"{Guid.NewGuid():N}.db");
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={databasePath};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });
        db.CodeFirst.InitTables<QueryEntity>();
        _clients.Add(db);
        return db;
    }

    public void Dispose()
    {
        foreach (SqlSugarClient client in _clients)
        {
            client.Close();
            client.Dispose();
        }

        if (Directory.Exists(_temporaryDirectory))
            Directory.Delete(_temporaryDirectory, recursive: true);
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        Assert.True(thread.TrySetApartmentState(ApartmentState.STA));
        thread.Start();
        thread.Join();

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [SugarTable("generic_query_test")]
    public sealed class QueryEntity : IEntity
    {
        [SugarColumn(ColumnName = "id", IsPrimaryKey = true)]
        public int Id { get; set; }

        [Display(Name = "层级")]
        [SugarColumn(ColumnName = "z_index")]
        public int ZIndex { get; set; }

        public bool Enabled { get; set; }
        public string Name { get; set; } = string.Empty;

        [SugarColumn(IsIgnore = true)]
        public string Ignored { get; set; } = string.Empty;

        [Browsable(false)]
        public string Hidden { get; set; } = string.Empty;
    }
}
