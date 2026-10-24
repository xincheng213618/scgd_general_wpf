using ColorVision.Database;
using SqlSugar;

namespace ColorVision.Engine.Services.Devices.SMU.Dao;

// Separate extension table: legacy queries/inserts must keep working against unmodified service databases.
[SugarTable(SmuLocalDetailsModel.TableName)]
public sealed class SmuLocalDetailsModel : EntityBase, IInitTables
{
    public const string TableName = "t_scgd_measure_result_smu_local_details";
    [SugarColumn(ColumnName = "is_scan")]
    public bool IsScan { get; set; }
    [SugarColumn(ColumnName = "result_id")]
    public int ResultId { get; set; }
    [SugarColumn(ColumnName = "details", ColumnDataType = "json", IsJson = true)]
    public string Details { get; set; } = string.Empty;
}
