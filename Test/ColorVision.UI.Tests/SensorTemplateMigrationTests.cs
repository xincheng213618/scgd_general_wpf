using ColorVision.Database;
using SqlSugar;

namespace ColorVision.UI.Tests;

public class SensorTemplateMigrationTests
{
    [Fact]
    public void DetachLegacySensorReferencesPreservesCommandsAndLeavesActualRoiTemplateAlone()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = "Data Source=:memory:", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        db.Ado.ExecuteCommand("""
            CREATE TABLE t_scgd_sys_dictionary_mod_master (id INTEGER PRIMARY KEY, code TEXT, mod_type INTEGER);
            CREATE TABLE t_scgd_sys_dictionary_mod_item (id INTEGER PRIMARY KEY, pid INTEGER, symbol TEXT, name TEXT,
                default_val TEXT, val_type INTEGER, value_range TEXT, create_date TEXT, is_enable INTEGER, is_delete INTEGER, remark TEXT);
            CREATE TABLE t_scgd_mod_param_master (id INTEGER PRIMARY KEY, mm_id INTEGER, name TEXT);
            CREATE TABLE t_scgd_mod_param_detail (id INTEGER PRIMARY KEY, pid INTEGER, cc_pid INTEGER, value_a TEXT, value_b TEXT);
            INSERT INTO t_scgd_sys_dictionary_mod_master VALUES (52, 'Image.ROI', 7), (1000, 'Sensor.CVFilterWheel', 5);
            INSERT INTO t_scgd_mod_param_master VALUES (80, 52, 'Sensor.CVFilterWheel1'), (81, 52, 'ROI'), (82, 1000, 'Filter wheel');
            INSERT INTO t_scgd_mod_param_detail VALUES (651, 80, 521, 'send', 'receive'), (652, 81, 522, 'roi', ''), (653, 82, 1001, 'turn', 'done');
            """);

        string sql = SensorTemplateMigrationSqlBuilder.Build(db);
        db.Ado.ExecuteCommand(sql);
        db.Ado.ExecuteCommand(sql);

        Assert.Equal(2, db.Ado.GetInt("SELECT COUNT(*) FROM t_scgd_mod_param_master WHERE mm_id IS NULL AND id IN (80, 82)"));
        Assert.Equal(52, db.Ado.GetInt("SELECT mm_id FROM t_scgd_mod_param_master WHERE id = 81"));
        Assert.Equal(522, db.Ado.GetInt("SELECT cc_pid FROM t_scgd_mod_param_detail WHERE id = 652"));
        Assert.Equal(2, db.Ado.GetInt("SELECT COUNT(*) FROM t_scgd_mod_param_detail WHERE cc_pid IS NULL AND id IN (651, 653)"));
        Assert.Equal("send/receive", db.Ado.GetString("SELECT value_a || '/' || value_b FROM t_scgd_mod_param_detail WHERE id = 651 AND pid = 80"));
        Assert.Equal("turn/done", db.Ado.GetString("SELECT value_a || '/' || value_b FROM t_scgd_mod_param_detail WHERE id = 653 AND pid = 82"));
    }

    [Theory]
    [InlineData(52, "Image.ROI", 7)]
    [InlineData(1000, "Sensor.CVFilterWheel", 5)]
    [InlineData(1001, "Sensor.Default", 5)]
    public void BuildPreservesSecondGroupPayloadWithoutReusingItsDictionaryId(int dictionaryId, string code, int modType)
    {
        string sql = SensorTemplateMigrationSqlBuilder.Build(
            [
                new SensorTemplateMigrationMasterRow
                {
                    ModMasterId = 80,
                    SourceDictionaryId = dictionaryId,
                    DictionaryCode = code,
                    DictionaryModType = modType
                }
            ],
            [new SensorTemplateMigrationDetailRow { DetailId = 651, ModMasterId = 80, SourceItemId = 521 }],
            []);

        Assert.Contains("SET `mm_id` = NULL WHERE `id` IN (80)", sql);
        Assert.Contains("SET `cc_pid` = NULL WHERE `pid` IN (80)", sql);
        Assert.DoesNotContain("INSERT INTO", sql);
        Assert.DoesNotContain("`value_a`", sql);
        Assert.DoesNotContain("`value_b`", sql);
    }

    [Theory]
    [InlineData("Sensor.Default", 5)]
    [InlineData(null, null)]
    public void BuildDefaultSensorReusesSeedAndRemapsCommandDefinitions(string? code, int? modType)
    {
        DateTime created = new(2026, 7, 17, 12, 0, 0, DateTimeKind.Local);
        string sql = SensorTemplateMigrationSqlBuilder.Build(
            [
                new SensorTemplateMigrationMasterRow
                {
                    ModMasterId = 90,
                    SourceDictionaryId = 50,
                    DictionaryCode = code,
                    DictionaryModType = modType
                }
            ],
            [new SensorTemplateMigrationDetailRow { DetailId = 901, ModMasterId = 90, SourceItemId = 900 }],
            [
                new SensorTemplateMigrationItemRow
                {
                    SourceItemId = 900,
                    SourceDictionaryId = 50,
                    Symbol = "open",
                    Name = "打开",
                    DefaultValue = "OPEN,OK,Ascii,1000/0,0",
                    ValueType = 3,
                    CreateDate = created,
                    IsEnable = true
                }
            ]);

        Assert.Contains("WHERE `id` = 50 AND `code` = 'Sensor.Default' AND `mod_type` = 5", sql);
        Assert.DoesNotContain("INSERT INTO `t_scgd_sys_dictionary_mod_master`", sql);
        Assert.DoesNotContain("UPDATE `t_scgd_sys_dictionary_mod_master`", sql);
        Assert.Contains("'open', '打开', 'OPEN,OK,Ascii,1000/0,0'", sql);
        Assert.Contains("WHERE `id` IN (90)", sql);
        Assert.Contains("WHERE `id` IN (901)", sql);
        Assert.Contains("AND `cc_pid` = 900", sql);
        Assert.Contains("COALESCE(MAX(`id`), 0) + 1", sql);
    }

    [Fact]
    public void BuildMissingCommandReferenceUsesSeedWithoutCreatingDefinitions()
    {
        string sql = SensorTemplateMigrationSqlBuilder.Build(
            [new SensorTemplateMigrationMasterRow { ModMasterId = 90, SourceDictionaryId = 50, DictionaryCode = "Sensor.Default", DictionaryModType = 5 }],
            [new SensorTemplateMigrationDetailRow { DetailId = 901, ModMasterId = 90, SourceItemId = 521 }],
            []);

        Assert.Contains("`pid` = @cv_sensor_dictionary_id AND `symbol` = 'defaultcommand'", sql);
        Assert.Contains("WHERE `id` IN (901)", sql);
        Assert.DoesNotContain("INSERT INTO", sql);
        Assert.DoesNotContain("`value_a`", sql);
    }
}
