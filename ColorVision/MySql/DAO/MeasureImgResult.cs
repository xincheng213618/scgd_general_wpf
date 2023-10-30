using System;
using System.Collections.Generic;
using System.Data;

namespace ColorVision.MySql.DAO
{
    public class MeasureImgResultModel : PKModel
    {

        public int BatchId { get; set; }

        public string? FileData { get; set; }

        public string? DeviceCode { get; set; }

        public DateTime? CreateDate { get; set; } = DateTime.Now;
    }


    public class MeasureImgResultDao : BaseDaoMaster<MeasureImgResultModel>
    {
        public MeasureImgResultDao() : base(string.Empty, "t_scgd_measure_result_img", "id", false)
        {
        }

        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add(new DataColumn("id", typeof(int)));
            dInfo.Columns.Add(new DataColumn("batch_id", typeof(int)));
            dInfo.Columns.Add(new DataColumn("file_data", typeof(string)));
            dInfo.Columns.Add(new DataColumn("device_code", typeof(string)));
            dInfo.Columns.Add(new DataColumn("create_date", typeof(DateTime)));
            return dInfo;
        }

        public override MeasureImgResultModel GetModel(DataRow item)
        {
            MeasureImgResultModel model = new MeasureImgResultModel
            {
                Id = item.Field<int>("id"),
                BatchId = item.Field<int?>("batch_id")??-1,
                FileData = item.Field<string?>("file_data"),
                DeviceCode = item.Field<string?>("device_code"),
                CreateDate = item.Field<DateTime?>("create_date"),
            };

            return model;
        }

        public override DataRow Model2Row(MeasureImgResultModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["batch_id"] = item.BatchId;
                row["file_data"] = item.FileData;
                row["device_code"] = item.DeviceCode;
                row["create_date"] = item.CreateDate;
            }
            return row;
        }
    }
}
