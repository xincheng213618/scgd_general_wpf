#pragma warning disable CA1707 // 标识符不应包含下划线

using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine.Services.Dao
{
    [@SugarTable("t_scgd_measure_result_img")]
    public class MeasureImgResultModel : PKModel, IInitTables
    {
        [SugarColumn(ColumnName ="batch_id")]
        public int BatchId { get; set; }
        [SugarColumn(ColumnName = "params" ,ColumnDataType ="json")]
        public string? ReqParams { get; set; }
        [SugarColumn(ColumnName ="raw_file")]
        public string? RawFile { get; set; }

        [SugarColumn(ColumnName ="file_data")]
        public string? ImgFrameInfo { get; set; }

        [SugarColumn(ColumnName ="file_type")]
        public sbyte? FileType { get; set; }

        [SugarColumn(ColumnName ="result_code")]
        public int ResultCode { get; set; }
        [SugarColumn(ColumnName ="total_time")]
        public int TotalTime { get; set; }

        [SugarColumn(ColumnName ="result")]
        public string? ResultMsg { get; set; } 

        [SugarColumn(ColumnName ="file_data")]
        public string? FileData { get; set; }

        [SugarColumn(ColumnName ="file_url")]
        public string? FileUrl { get; set; }

        [SugarColumn(ColumnName ="device_code")]
        public string? DeviceCode { get; set; } 

        [SugarColumn(ColumnName ="create_date")]
        public DateTime? CreateDate { get; set; }
    }



    public class MeasureImgResultDao : BaseTableDao<MeasureImgResultModel>
    {

        public static MeasureImgResultDao Instance { get;} = new MeasureImgResultDao();
    }
}
