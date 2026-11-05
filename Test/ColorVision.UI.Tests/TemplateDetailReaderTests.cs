using ColorVision.Engine;
using ColorVision.Engine.Templates;
using SqlSugar;
using System.Text.RegularExpressions;

namespace ColorVision.UI.Tests;

public sealed class TemplateDetailReaderTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(500, 1)]
    [InlineData(501, 2)]
    public void PrefetchIsBoundedAndPreservesEveryDetailRecord(int parentCount, int expectedQueries)
    {
        using var db = CreateDatabase();
        int[] parents = Enumerable.Range(1, parentCount).Reverse().ToArray();
        ModDetailModel[] records = parents.SelectMany(parent => new[]
        {
            new ModDetailModel { Id = parent * 2, Pid = parent, SysPid = parent * 2, ValueA = "{\"future\":42}", ValueB = "previous", IsEnable = false, IsDelete = true },
            new ModDetailModel { Id = parent * 2 + 1, Pid = parent, SysPid = parent * 2 + 1, ValueA = null, ValueB = null }
        }).Append(new ModDetailModel { Id = 2000, Pid = -77, SysPid = -77, ValueA = "outside scope" }).ToArray();
        db.Insertable(records).ExecuteCommand();
        var expected = parents.ToDictionary(parent => parent,
            parent => db.Queryable<ModDetailModel>().Where(item => item.Pid == parent).ToList());
        List<string> queries = [];
        db.Aop.OnLogExecuting = (sql, _) => queries.Add(sql);

        var reader = new TemplateDetailReader(db, parents.Concat(parents.Take(1)),
            field => field % 2 == 0 ? null : "field-" + field, preserveDetailReadOrder: false);
        List<int> consumedParents = [];
        foreach (int parent in parents)
        {
            List<ModDetailModel> actual = reader.Read(parent);
            Assert.Equal(expected[parent].Select(Record), actual.Select(Record));
            Assert.All(actual, row => Assert.Equal(parent, row.Pid));
            consumedParents.Add(parent);
        }

        Assert.Equal(parents, consumedParents);
        Assert.Equal(expectedQueries, queries.Count);
        Assert.DoesNotContain(queries, sql => sql.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AmbiguousFieldsUseTheOriginalQueryAndEmptyGroupsDoNotResolveSymbols()
    {
        using var db = CreateDatabase();
        db.Insertable(new[]
        {
            Detail(1, 1, 100), Detail(2, 1, 100), // Duplicate dictionary item, even without a known symbol.
            Detail(3, 2, 101), Detail(4, 2, 102), // Different items with the same symbol.
            Detail(5, 3, 103), Detail(6, 3, 104), // Unknown symbols must remain in the returned collection.
            Detail(7, 4, 105), Detail(8, 4, 106), // ModelBase accepts the empty string as a key.
            Detail(9, 5, 107), Detail(10, 5, 108), // Symbol matching is case-sensitive.
            Detail(11, 7, 109),
        }).ExecuteCommand();
        Dictionary<int, string?> symbols = new()
        {
            [101] = "same", [102] = "same", [105] = "", [106] = "", [107] = "Key", [108] = "key"
        };
        List<int> resolvedFields = [];
        List<int> individualQueries = [];
        int batchQueries = 0;
        db.Aop.OnLogExecuting = (sql, parameters) =>
        {
            if (IsBatch(sql)) batchQueries++;
            else individualQueries.Add(Convert.ToInt32(Assert.Single(parameters).Value));
        };
        var reader = new TemplateDetailReader(db, Enumerable.Range(1, 7), field =>
        {
            resolvedFields.Add(field);
            return symbols.GetValueOrDefault(field);
        }, preserveDetailReadOrder: false);
        Assert.Empty(resolvedFields);

        Assert.Empty(reader.Read(6));
        Assert.Equal(109, Assert.Single(reader.Read(7)).SysPid);
        Assert.Empty(resolvedFields);
        for (int parent = 1; parent <= 5; parent++)
            Assert.Equal(new[] { parent * 2 - 1, parent * 2 }, reader.Read(parent).Select(row => row.Id));

        Assert.Equal(1, batchQueries);
        Assert.Equal(new[] { 1, 2, 4 }, individualQueries);
        Assert.DoesNotContain(100, resolvedFields);
        Assert.Equal(new[] { 101, 102, 103, 104, 105, 106, 107, 108 }, resolvedFields);
    }

    [Fact]
    public void AFailedLaterBatchFallsBackAtTheParentBoundaryAfterEarlierProgress()
    {
        using var db = CreateDatabase();
        int[] parents = Enumerable.Range(1, 501).Reverse().ToArray();
        db.Insertable(parents.Select(parent => Detail(parent, parent, parent)).ToArray()).ExecuteCommand();
        int batches = 0;
        List<int> individualQueries = [];
        db.Aop.OnLogExecuting = (sql, parameters) =>
        {
            if (IsBatch(sql))
            {
                if (++batches == 2) throw new InvalidOperationException("batch-fixture-failure");
            }
            else
            {
                int parent = Convert.ToInt32(Assert.Single(parameters).Value);
                individualQueries.Add(parent);
                if (parent == 1) throw new InvalidOperationException("parent-fixture-failure");
            }
        };
        var reader = new TemplateDetailReader(db, parents,
            _ => throw new InvalidOperationException("Single-row groups must not resolve symbols."), preserveDetailReadOrder: false);
        List<int> published = [];

        Exception failure = Assert.ThrowsAny<Exception>(() =>
        {
            foreach (int parent in parents)
            {
                _ = reader.Read(parent);
                published.Add(parent);
            }
        });

        Assert.Contains("parent-fixture-failure", failure.ToString());
        Assert.Equal(parents[..^1], published);
        Assert.Equal(2, batches);
        Assert.Equal(new[] { 1 }, individualQueries);
    }

    [Fact]
    public void FailedSymbolProbesFallBackWithoutSuppressingTheOriginalReadFailure()
    {
        using var db = CreateDatabase();
        db.Insertable(new[] { Detail(1, 1, 1), Detail(2, 1, 2) }).ExecuteCommand();
        int individualQueries = 0;
        db.Aop.OnLogExecuting = (sql, _) =>
        {
            if (!IsBatch(sql))
            {
                individualQueries++;
                throw new InvalidOperationException("parent-fixture-failure");
            }
        };
        var reader = new TemplateDetailReader(db, [1],
            _ => throw new InvalidOperationException("symbol-fixture-failure"), preserveDetailReadOrder: false);

        Exception failure = Assert.ThrowsAny<Exception>(() => reader.Read(1));
        Assert.Contains("parent-fixture-failure", failure.ToString());
        Assert.Equal(1, individualQueries);
    }

    [Fact]
    public void OrderedCollectionsSkipPrefetchAndRetainPerParentReadsAndFailureProgress()
    {
        using var db = CreateDatabase();
        db.Insertable(new[]
        {
            Detail(32, 3, 301), Detail(31, 3, 302), // Distinct symbols can still form an ordered command list.
            Detail(12, 1, 101), Detail(11, 1, 102),
            Detail(21, 2, 201)
        }).ExecuteCommand();
        int[] parents = [3, 1, 2];
        var expected = parents.ToDictionary(parent => parent,
            parent => db.Queryable<ModDetailModel>().Where(item => item.Pid == parent).ToList());
        int enumeratedParents = 0, resolvedFields = 0;
        IEnumerable<int> ParentIds()
        {
            foreach (int parent in parents)
            {
                enumeratedParents++;
                yield return parent;
            }
        }
        List<int> queriedParents = [];
        List<string> queries = [];
        db.Aop.OnLogExecuting = (sql, parameters) =>
        {
            queries.Add(sql);
            int parent = Convert.ToInt32(Assert.Single(parameters).Value);
            queriedParents.Add(parent);
            if (parent == 2) throw new InvalidOperationException("ordered-parent-fixture-failure");
        };
        var reader = new TemplateDetailReader(db, ParentIds(), field =>
        {
            resolvedFields++;
            return "field-" + field;
        }, preserveDetailReadOrder: true);
        Assert.Empty(queries);
        Assert.Equal(0, enumeratedParents);
        List<int> published = [];

        Exception failure = Assert.ThrowsAny<Exception>(() =>
        {
            foreach (int parent in parents)
            {
                Assert.Equal(expected[parent].Select(Record), reader.Read(parent).Select(Record));
                published.Add(parent);
            }
        });

        Assert.Contains("ordered-parent-fixture-failure", failure.ToString());
        Assert.Equal(new[] { 3, 1 }, published);
        Assert.Equal(parents, queriedParents);
        Assert.DoesNotContain(queries, IsBatch);
        Assert.DoesNotContain(queries, sql => sql.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, resolvedFields);
        Assert.Equal(0, enumeratedParents);
    }

    private static (int Id, int Pid, int SysPid, string? ValueA, string? ValueB, bool Enabled, bool Deleted) Record(ModDetailModel row)
        => (row.Id, row.Pid, row.SysPid, row.ValueA, row.ValueB, row.IsEnable, row.IsDelete);

    private static ModDetailModel Detail(int id, int parent, int field) => new() { Id = id, Pid = parent, SysPid = field, ValueA = "value-" + id };
    private static bool IsBatch(string sql) => Regex.IsMatch(sql, @"\bIN\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static SqlSugarClient CreateDatabase()
    {
        var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = "Data Source=:memory:", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        db.CodeFirst.InitTables<ModDetailModel>();
        db.Ado.ExecuteCommand("CREATE INDEX ix_template_detail_fixture_pid ON t_scgd_mod_param_detail(pid)");
        return db;
    }
}
