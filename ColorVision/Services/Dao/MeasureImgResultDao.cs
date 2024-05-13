#pragma warning disable CA1707 // 标识符不应包含下划线

using System;
using System.Collections.Generic;
using System.Data;
using ColorVision.MySql;
using ColorVision.MySql.ORM;
using NPOI.SS.Formula.Functions;

namespace ColorVision.Services.Dao
{
    public class MeasureImgResultModel : PKModel
    {

        public int BatchId { get; set; }
        public string? BatchCode { get; set; }
        public string? ReqParams { get; set; }
        public string? RawFile { get; set; }
        public string? ImgFrameInfo { get; set; }
        public sbyte? FileType { get; set; }
        public int ResultCode { get; set; }
        public int TotalTime { get; set; }
        public string? ResultMsg { get; set; }

        public string? FileData { get; set; }
        public string? FileUrl { get; set; }

        public string? DeviceCode { get; set; }

        public System.DateTime? CreateDate { get; set; } = DateTime.Now;
    }


    public class MeasureImgResultDao : BaseDaoMaster<MeasureImgResultModel>
    {


        public MeasureImgResultDao() : base("v_scgd_measure_result_img", "t_scgd_measure_result_img", "id", false)
        {

        }

        public override MeasureImgResultModel GetModelFromDataRow(DataRow item)
        {
            MeasureImgResultModel model = new()
            {
                Id = item.Field<int>("id"),
                BatchId = item.Field<int?>("batch_id") ?? -1,
                BatchCode = item.Field<string?>("batch_code"),
                RawFile = item.Field<string?>("raw_file"),
                FileType = item.Field<sbyte?>("file_type"),
                FileData = item.Field<string?>("file_data"),
                ResultCode = item.Field<int>("result_code"),
                TotalTime = item.Field<int>("total_time"),
                ResultMsg = item.Field<string?>("result"),
                ReqParams = item.Field<string?>("params"),
                ImgFrameInfo = item.Field<string?>("file_data"),
                FileUrl = item.Field<string?>("file_url"),
                DeviceCode = item.Field<string?>("device_code"),
                CreateDate = item.Field<System.DateTime?>("create_date"),
            };
            return model;
        }

        public List<MeasureImgResultModel> GetAllDevice(string devcode) => ConditionalQuery(new Dictionary<string, Object>() { { "device_code", devcode } });


        public List<MeasureImgResultModel> ConditionalQuery(string id, string batch_code, string file_url, string device_code,DateTime dateTimeSTART, DateTime dateTimeEnd)
        {
            Dictionary<string, object> keyValuePairs = new(0);
            keyValuePairs.Add("id", id);
            keyValuePairs.Add("batch_code", batch_code);
            keyValuePairs.Add("raw_file", file_url);
            keyValuePairs.Add("device_code", device_code);
            keyValuePairs.Add(">create_date", dateTimeSTART);
            keyValuePairs.Add("<create_date", dateTimeEnd);
            //业务要求，取图失败的时候的记录不显示
            //keyValuePairs.Add("result_code", "0");
            return ConditionalQuery(keyValuePairs);
        }
    }
}
