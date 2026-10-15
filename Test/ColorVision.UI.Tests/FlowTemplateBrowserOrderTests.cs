using ColorVision.Engine.Templates;
using SqlSugar;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class FlowTemplateBrowserOrderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SwapKeepsEveryMasterFieldAndMovesAllDetailsWithItsTemplate(bool poi)
    {
        using var db = CreateDb(poi, out var master, out var detail);
        string original = Snapshot(db, master, detail);
        var fields10 = db.Ado.GetString($"SELECT name || code || cfg_json || create_date || remark FROM {master} WHERE id=10");
        var fields20 = db.Ado.GetString($"SELECT name || code || cfg_json || create_date || remark FROM {master} WHERE id=20");
        TemplateOrderSwap.SwapRecords(db, poi, 10, 20, "First", "Second");
        Assert.Equal(fields20, db.Ado.GetString($"SELECT name || code || cfg_json || create_date || remark FROM {master} WHERE id=10"));
        Assert.Equal(fields10, db.Ado.GetString($"SELECT name || code || cfg_json || create_date || remark FROM {master} WHERE id=20"));
        Assert.Equal(new[] { 20, 20, 10, 30 }, db.Ado.SqlQuery<int>($"SELECT pid FROM {detail} ORDER BY id"));
        Assert.Equal(new[] { "resource-a", "parameter-a", "resource-b", "untouched" }, db.Ado.SqlQuery<string>($"SELECT value_a FROM {detail} ORDER BY id"));
        Assert.Equal(0, db.Ado.GetInt($"SELECT COUNT(*) FROM {master} WHERE id<0"));
        TemplateOrderSwap.SwapRecords(db, poi, 10, 20, "Second", "First");
        Assert.Equal(original, Snapshot(db, master, detail));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FailureAfterMovingMasterIdsRollsBackTheEntireSwap(bool poi, bool ignoreMasterUpdate)
    {
        using var db = CreateDb(poi, out var master, out var detail);
        db.Ado.ExecuteCommand(ignoreMasterUpdate
            ? $"CREATE TRIGGER fail_swap BEFORE UPDATE ON {master} WHEN OLD.id=20 BEGIN SELECT RAISE(IGNORE); END"
            : $"CREATE TRIGGER fail_swap BEFORE UPDATE ON {detail} BEGIN SELECT RAISE(ABORT, 'blocked detail update'); END");
        string original = Snapshot(db, master, detail);
        Assert.ThrowsAny<Exception>(() => TemplateOrderSwap.SwapRecords(db, poi, 10, 20, "First", "Second"));
        Assert.Equal(original, Snapshot(db, master, detail));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeletedOrRenamedRecordsAreRejectedBeforeAnyWrite(bool poi)
    {
        using var db = CreateDb(poi, out var master, out var detail);
        string original = Snapshot(db, master, detail);
        Assert.Throws<InvalidOperationException>(() => TemplateOrderSwap.SwapRecords(db, poi, 10, 20, "Old name", "Second"));
        Assert.Throws<InvalidOperationException>(() => TemplateOrderSwap.SwapRecords(db, poi, 10, 999, "First", "Missing"));
        Assert.Equal(original, Snapshot(db, master, detail));
    }

    private static SqlSugarClient CreateDb(bool poi, out string master, out string detail)
    {
        string path = Path.Combine(Directory.CreateTempSubdirectory("ColorVision-template-order-").FullName, "order.db");
        var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source={path}", DbType = DbType.Sqlite, IsAutoCloseConnection = true });
        master = poi ? "t_scgd_algorithm_poi_template_master" : "t_scgd_mod_param_master";
        detail = poi ? "t_scgd_algorithm_poi_template_detail" : "t_scgd_mod_param_detail";
        db.Ado.ExecuteCommand($"CREATE TABLE {master}(id INTEGER PRIMARY KEY,name TEXT,code TEXT,cfg_json TEXT,create_date TEXT,remark TEXT)");
        db.Ado.ExecuteCommand($"CREATE TABLE {detail}(id INTEGER PRIMARY KEY,pid INTEGER,value_a TEXT)");
        db.Ado.ExecuteCommand($"INSERT INTO {master} VALUES(10,'First','code-a','{{\"a\":1}}','2025-01-01','a'),(20,'Second','code-b','{{\"b\":2}}','2026-02-02','b'),(30,'Other','other','{{}}','2024-03-03','keep')");
        db.Ado.ExecuteCommand($"INSERT INTO {detail} VALUES(1,10,'resource-a'),(2,10,'parameter-a'),(3,20,'resource-b'),(4,30,'untouched')");
        return db;
    }

    private static string Snapshot(SqlSugarClient db, string master, string detail)
        => string.Join("|", db.Ado.SqlQuery<string>($"SELECT id || name || code || cfg_json || create_date || remark FROM {master} ORDER BY id"))
            + string.Join("|", db.Ado.SqlQuery<string>($"SELECT id || ':' || pid || ':' || value_a FROM {detail} ORDER BY id"));
}
