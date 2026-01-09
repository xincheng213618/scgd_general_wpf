using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine
{
    [@SugarTable("t_scgd_algorithm_result_master")]
    public class AlgResultMasterModel : EntityBase,IInitTables
    {
        public AlgResultMasterModel() { }

        [SugarColumn(ColumnName ="tid", IsNullable = true)]
        public int? TId { get; set; }

        [SugarColumn(ColumnName ="tname", IsNullable = true)]
        public string TName { get; set; }

        [SugarColumn(ColumnName ="img_file", IsNullable = true)]
        public string ImgFile { get; set; }

        [SugarColumn(ColumnName ="img_file_type",ColumnDataType = "tinyint",Length =4)]
        public ViewResultAlgType ImgFileType { get; set; }

        [SugarColumn(ColumnName ="version", IsNullable = true)]
        public string version { get; set; }

        [SugarColumn(ColumnName = "nd_port", IsNullable = true, ColumnDescription = "ND滤轮")]
        public bool? NDPort { get; set; }

        [SugarColumn(ColumnName ="batch_id", IsNullable = true)]
        public int? BatchId { get; set; }

        [SugarColumn(ColumnName = "z_index", IsNullable = true)]
        public int? Zindex { get; set; }

        [SugarColumn(ColumnName ="params", IsNullable = true, ColumnDataType = "json")]
        public string Params { get; set; }

        [SugarColumn(ColumnName = "device_code", IsNullable = true)]
        public string DeviceCode { get; set; }

        [SugarColumn(ColumnName = "smu_data_id",IsNullable =true)]
        public int? SMUDataID { get; set; }

        [SugarColumn(ColumnName ="result_code", IsNullable = true)]
        public int? ResultCode { get; set; }

        [SugarColumn(ColumnName ="result", IsNullable = true)]
        public string Result { get; set; }

        [SugarColumn(ColumnName ="img_result", IsNullable = true)]
        public string ResultImagFile { get; set; }

        [SugarColumn(ColumnName ="total_time",IsNullable =true)]
        public int TotalTime { get; set; }

        [SugarColumn(ColumnName ="create_date")]
        public DateTime? CreateDate { get; set; }

    }


    public class AlgResultMasterDao : BaseTableDao<AlgResultMasterModel>
    {
        public static AlgResultMasterDao Instance { get; set; } = new AlgResultMasterDao();
    }
}
