using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine
{
    [@SugarTable("t_scgd_algorithm_result_master")]
    public class AlgResultMasterModel : EntityBase,IInitTables
    {
        public AlgResultMasterModel() { }

        [SugarColumn(ColumnName = "tid", IsNullable = true, ColumnDescription = "参数模板ID")]
        public int? TId { get; set; }

        [SugarColumn(ColumnName = "tname", IsNullable = true, ColumnDescription = "参数模板名称")]
        public string? TName { get; set; }

        [SugarColumn(ColumnName = "img_file", IsNullable = true)]
        public string? ImgFile { get; set; }

        [SugarColumn(ColumnName = "img_file_type", ColumnDataType = "int", Length = 11, ColumnDescription = "0-关注点;1-色度;2-亮度;3-FOV;4-SFR;5-MTF;6-Ghost;7-LedCheck;8-LightArea;9-Distortion;10-Calibration;11-BuildPOI")]
        public ViewResultAlgType ImgFileType { get; set; }

        [SugarColumn(ColumnName = "version", Length = 16, IsNullable = true, ColumnDescription = "版本号")]
        public string? version { get; set; }

        [SugarColumn(ColumnName = "nd_port", ColumnDataType = "tinyint", Length = 2, IsNullable = true, ColumnDescription = "ND滤轮")]
        public int? NDPort { get; set; }

        [SugarColumn(ColumnName ="batch_id", IsNullable = true)]
        public int? BatchId { get; set; }

        [SugarColumn(ColumnName = "z_index", IsNullable = true)]
        public int? Zindex { get; set; }

        [SugarColumn(ColumnName = "params", IsNullable = true, ColumnDataType = "json", ColumnDescription = "参数")]
        public string? Params { get; set; }

        [SugarColumn(ColumnName = "device_code", IsNullable = true)]
        public string? DeviceCode { get; set; }

        [SugarColumn(ColumnName = "smu_data_id", IsNullable = true, ColumnDescription = "SMU result ID")]
        public int? SMUDataID { get; set; }

        [SugarColumn(ColumnName = "i_result", IsNullable = true, ColumnDescription = "源表电流")]
        public float? IResult { get; set; }

        [SugarColumn(ColumnName = "v_result", IsNullable = true, ColumnDescription = "源表电压")]
        public float? VResult { get; set; }

        [SugarColumn(ColumnName ="result_code", IsNullable = true)]
        public int? ResultCode { get; set; }

        [SugarColumn(ColumnName = "result", IsNullable = true)]
        public string? Result { get; set; }

        [SugarColumn(ColumnName = "img_result", IsNullable = true, ColumnDescription = "图像显示输出结果")]
        public string? ResultImagFile { get; set; }

        [SugarColumn(ColumnName = "total_time", IsNullable = true, ColumnDescription = "总用时")]
        public int TotalTime { get; set; }

        [SugarColumn(ColumnName = "create_date", IsNullable = false, DefaultValue = "CURRENT_TIMESTAMP", ColumnDescription = "创建日期")]
        public DateTime? CreateDate { get; set; }

    }


    public class AlgResultMasterDao : BaseTableDao<AlgResultMasterModel>
    {
        public static AlgResultMasterDao Instance { get; set; } = new AlgResultMasterDao();
    }
}
