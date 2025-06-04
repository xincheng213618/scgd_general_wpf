#pragma warning disable CA1707 // 标识符不应包含下划线

using ColorVision.Engine.MySql.ORM;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Dao
{
    [Table("t_scgd_measure_result_img")]
    public class MeasureImgResultModel : PKModel
    {
        [Column("batch_id")]
        public int BatchId { get; set; }
        [Column("params")]
        public string? ReqParams { get; set; }
        [Column("raw_file")]
        public string? RawFile { get; set; }

        [Column("file_data")]
        public string? ImgFrameInfo { get; set; }

        [Column("file_type")]
        public sbyte? FileType { get; set; }
        [Column("result_code")]
        public int ResultCode { get; set; }
        [Column("total_time")]
        public int TotalTime { get; set; }
        [Column("result")]
        public string? ResultMsg { get; set; }

        [Column("file_data")]
        public string? FileData { get; set; }

        [Column("file_url")]
        public string? FileUrl { get; set; }

        [Column("device_code")]
        public string? DeviceCode { get; set; }

        [Column("create_date")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;
    }



    public class MeasureImgResultDao : BaseTableDao<MeasureImgResultModel>
    {

        public static MeasureImgResultDao Instance { get;} = new MeasureImgResultDao();


        public List<MeasureImgResultModel> GetAllDevice(string devcode,int limit) => ConditionalQuery(new Dictionary<string, Object>() { { "device_code", devcode } }, limit);


        public List<MeasureImgResultModel> ConditionalQuery(string id, string file_url, string device_code,DateTime dateTimeSTART, DateTime dateTimeEnd, int limit =-1)
        {
            Dictionary<string, object> keyValuePairs = new(0);
            keyValuePairs.Add("id", id);
            keyValuePairs.Add("raw_file", file_url);
            keyValuePairs.Add("device_code", device_code);
            keyValuePairs.Add(">create_date", dateTimeSTART);
            keyValuePairs.Add("<create_date", dateTimeEnd);
            //业务要求，取图失败的时候的记录不显示
            //Templates .Add("result_code", "0");
            return ConditionalQuery(keyValuePairs, limit);
        }
    }
}
