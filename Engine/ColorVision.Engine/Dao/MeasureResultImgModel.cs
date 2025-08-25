using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine
{
    [@SugarTable("t_scgd_measure_result_img")]
    public class MeasureResultImgModel : PKModel, IInitTables
    {
        [SugarColumn(ColumnName ="batch_id")]
        public int BatchId { get; set; }

        [SugarColumn(ColumnName = "params" ,ColumnDataType ="json")]
        public string? Params { get; set; }

        [SugarColumn(ColumnName ="raw_file")]
        public string? RawFile { get; set; }

        [SugarColumn(ColumnName ="file_url")]
        public string? FileUrl { get; set; }

        [SugarColumn(ColumnName = "file_type")]
        public sbyte? FileType { get; set; }

        [SugarColumn(ColumnName = "file_data", ColumnDataType = "json")]
        public string? ImgFrameInfo { get; set; }

        [SugarColumn(ColumnName = "result_code")]
        public int ResultCode { get; set; }

        [SugarColumn(ColumnName = "result")]
        public string? Result { get; set; }


        [SugarColumn(ColumnName = "total_time")]
        public int TotalTime { get; set; }


        [SugarColumn(ColumnName ="device_code",IsNullable =true)]
        public string? DeviceCode { get; set; }

        [SugarColumn(ColumnName = "create_date")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;
    }



    public class MeasureImgResultDao : BaseTableDao<MeasureResultImgModel>
    {

        public static MeasureImgResultDao Instance { get;} = new MeasureImgResultDao();
    }
}
