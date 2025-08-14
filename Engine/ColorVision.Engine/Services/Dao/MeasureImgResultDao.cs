#pragma warning disable CA1707 // 标识符不应包含下划线

using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Dao
{
    [SugarTable("t_scgd_measure_result_img")]
    [Table("t_scgd_measure_result_img")]
    public class MeasureImgResultModel : PKModel
    {
        [SugarColumn(ColumnName = "batch_id")]
        [Column("batch_id")]
        public int BatchId { get; set; }
        [SugarColumn(ColumnName = "params")]
        [Column("params")]
        public string? ReqParams { get; set; }
        [SugarColumn(ColumnName = "raw_file")]
        [Column("raw_file")]
        public string? RawFile { get; set; }

        [SugarColumn(ColumnName = "file_data")]
        [Column("file_data")]
        public string? ImgFrameInfo { get; set; }

        [SugarColumn(ColumnName = "file_type")]
        [Column("file_type")]
        public sbyte? FileType { get; set; }

        [SugarColumn(ColumnName = "result_code")]
        [Column("result_code")]
        public int ResultCode { get; set; }
        [SugarColumn(ColumnName = "total_time")]
        [Column("total_time")]
        public int TotalTime { get; set; }

        [SugarColumn(ColumnName = "result")]
        [Column("result")]
        public string? ResultMsg { get; set; }

        [SugarColumn(ColumnName = "file_data")]
        [Column("file_data")]
        public string? FileData { get; set; }

        [SugarColumn(ColumnName = "file_url")]
        [Column("file_url")]
        public string? FileUrl { get; set; }

        [SugarColumn(ColumnName = "device_code")]
        [Column("device_code")]
        public string? DeviceCode { get; set; }

        [SugarColumn(ColumnName = "create_date")]
        [Column("create_date")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;
    }



    public class MeasureImgResultDao : BaseTableDao<MeasureImgResultModel>
    {

        public static MeasureImgResultDao Instance { get;} = new MeasureImgResultDao();
    }
}
