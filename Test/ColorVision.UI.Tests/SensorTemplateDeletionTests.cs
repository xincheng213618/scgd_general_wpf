using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Sensor.Templates;
using SqlSugar;

namespace ColorVision.UI.Tests;

public class SensorTemplateDeletionTests
{
    [Fact]
    public void DeletingTypeRemovesOnlyItsTemplatesCommandsAndDefinitions()
    {
        using var db = CreateDatabase();
        SensorTemplateDictionaryService.DeleteType(db, 1, "Sensor.Test");
        Assert.False(db.Queryable<SysDictionaryModModel>().Any(item => item.Id == 1));
        Assert.False(db.Queryable<ModMasterModel>().Any(item => item.Pid == 1));
        Assert.False(db.Queryable<ModDetailModel>().Any(item => item.Pid == 10));
        Assert.False(db.Queryable<SysDictionaryModDetaiModel>().Any(item => item.PId == 1));
        Assert.True(db.Queryable<SysDictionaryModModel>().Any(item => item.Id == 2));
        Assert.True(db.Queryable<ModMasterModel>().Any(item => item.Id == 20));
        Assert.True(db.Queryable<ModDetailModel>().Any(item => item.Id == 200));
        Assert.True(db.Queryable<SysDictionaryModDetaiModel>().Any(item => item.Id == 2000));
    }

    [Theory]
    [InlineData("device")]
    [InlineData("shared")]
    [InlineData("failure")]
    [InlineData("wrong-type")]
    public void ReferencedOrFailedDeletionLeavesTheWholeTypeIntact(string scenario)
    {
        using var db = CreateDatabase();
        if (scenario == "device") db.Ado.ExecuteCommand("INSERT INTO t_scgd_sys_resource(id,name,code,type,txt_value,is_delete) VALUES(1,'Device','Device',5,'{\"Category\":\"Sensor.Test\"}',0)");
        if (scenario == "shared") db.Ado.ExecuteCommand("UPDATE t_scgd_mod_param_detail SET cc_pid=1000 WHERE id=200");
        if (scenario == "wrong-type") db.Ado.ExecuteCommand("UPDATE t_scgd_sys_dictionary_mod_master SET mod_type=4 WHERE id=1");
        if (scenario == "failure") db.Ado.ExecuteCommand("CREATE TRIGGER block_type_delete BEFORE DELETE ON t_scgd_sys_dictionary_mod_master BEGIN SELECT RAISE(ABORT,'simulated failure'); END");
        Assert.ThrowsAny<Exception>(() => SensorTemplateDictionaryService.DeleteType(db, 1, "Sensor.Test"));
        Assert.True(db.Queryable<SysDictionaryModModel>().Any(item => item.Id == 1));
        Assert.True(db.Queryable<ModMasterModel>().Any(item => item.Id == 10));
        Assert.True(db.Queryable<ModDetailModel>().Any(item => item.Id == 100));
        Assert.True(db.Queryable<SysDictionaryModDetaiModel>().Any(item => item.Id == 1000));
    }

    private static SqlSugarClient CreateDatabase()
    {
        var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = "Data Source=:memory:", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        // Only the columns consumed by this repository operation are needed; no real database is opened.
        db.Ado.ExecuteCommand("CREATE TABLE t_scgd_sys_dictionary_mod_master(id INTEGER PRIMARY KEY, code TEXT, mod_type INTEGER)");
        db.Ado.ExecuteCommand("CREATE TABLE t_scgd_mod_param_master(id INTEGER PRIMARY KEY, mm_id INTEGER)");
        db.Ado.ExecuteCommand("CREATE TABLE t_scgd_sys_dictionary_mod_item(id INTEGER PRIMARY KEY, pid INTEGER)");
        db.Ado.ExecuteCommand("CREATE TABLE t_scgd_mod_param_detail(id INTEGER PRIMARY KEY, pid INTEGER, cc_pid INTEGER)");
        db.Ado.ExecuteCommand("CREATE TABLE t_scgd_sys_resource(id INTEGER PRIMARY KEY, name TEXT, code TEXT, type INTEGER, txt_value TEXT, is_delete INTEGER)");
        db.Ado.ExecuteCommand("INSERT INTO t_scgd_sys_dictionary_mod_master VALUES(1,'Sensor.Test',5),(2,'Sensor.Other',5)");
        db.Ado.ExecuteCommand("INSERT INTO t_scgd_mod_param_master VALUES(10,1),(20,2)");
        db.Ado.ExecuteCommand("INSERT INTO t_scgd_sys_dictionary_mod_item VALUES(1000,1),(2000,2)");
        db.Ado.ExecuteCommand("INSERT INTO t_scgd_mod_param_detail VALUES(100,10,1000),(200,20,2000)");
        return db;
    }
}
