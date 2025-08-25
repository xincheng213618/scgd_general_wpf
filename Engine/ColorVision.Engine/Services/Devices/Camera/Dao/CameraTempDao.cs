using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine.Services.Dao
{
    [SugarTable("t_scgd_camera_temp")]
    public class CameraTempModel : VPKModel, IInitTables
    {
        [SugarColumn(ColumnName ="temp_value")]
        public float? TempValue { get; set; }
        [SugarColumn(ColumnName ="pwm_value")]
        public int? PwmValue { get; set; }
        [SugarColumn(ColumnName ="create_date")]

        public DateTime? CreateDate { get; set; }

        [SugarColumn(ColumnName ="res_id")]
        public int? RescourceId { get; set; }
    }
}
