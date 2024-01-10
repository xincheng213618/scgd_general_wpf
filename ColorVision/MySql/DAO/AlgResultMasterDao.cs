#pragma warning disable CS8601
using MQTTMessageLib.Algorithm;
using System;
using System.Collections.Generic;
using System.Data;

namespace ColorVision.MySql.DAO
{
    public class AlgResultMasterModel : PKModel
    {
        public AlgResultMasterModel() { }
        public AlgResultMasterModel(int? tid, string tname, string imgFile, string pms, AlgorithmResultType imgFileType, int? batchId, int? resultCode, string result)
        {
            TId = tid;
            TName = tname;
            ImgFile = imgFile;
            Params = pms;
            ImgFileType = imgFileType;
            BatchId = batchId;
            Result = result;
            ResultCode = resultCode;
            CreateDate = DateTime.Now;
        }

        public int? TId { get; set; }
        public string TName { get; set; }
        public string ImgFile { get; set; }
        /// <summary>
        /// 0-色度;1-亮度
        /// </summary>
        public AlgorithmResultType ImgFileType { get; set; }
        public int? BatchId { get; set; }
        public string BatchCode { get; set; }
        public string Params { get; set; }
        public int? ResultCode { get; set; }
        public string Result { get; set; }
        public long TotalTime { get; set; }
        public DateTime? CreateDate { get; set; }
    }
    public class AlgResultMasterDao : BaseDaoMaster<AlgResultMasterModel>
    {
        public AlgResultMasterDao() : base("v_scgd_algorithm_result_master", "t_scgd_algorithm_result_master", "id", false)
        {
        }

        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add("id", typeof(int));
            dInfo.Columns.Add("tid", typeof(int));
            dInfo.Columns.Add("img_file", typeof(string));
            dInfo.Columns.Add("tname", typeof(string));
            dInfo.Columns.Add("img_file_type", typeof(sbyte));
            dInfo.Columns.Add("result_code", typeof(int));
            dInfo.Columns.Add("result", typeof(string));
            dInfo.Columns.Add("params", typeof(string));

            dInfo.Columns.Add("total_time", typeof(int));
            dInfo.Columns.Add("batch_id", typeof(int));
            dInfo.Columns.Add("create_date", typeof(DateTime));
            return dInfo;
        }

        public List<AlgResultMasterModel> ConditionalQuery(string id, string batchid, string ImageType, string fileName)
        {
            Dictionary<string, object> keyValuePairs = new Dictionary<string, object>(0);
            keyValuePairs.Add("id", id);
            keyValuePairs.Add("batch_id", batchid);
            keyValuePairs.Add("img_file_type", ImageType);
            keyValuePairs.Add("img_file", fileName);
            return ConditionalQuery(keyValuePairs);
        }

        public override DataRow Model2Row(AlgResultMasterModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["tid"] = item.TId;
                if (item.ImgFile != null) row["img_file"] = item.ImgFile;
                if (item.TName != null) row["tname"] = item.TName;
                if (item.Result != null) row["result"] = item.Result;
                if (item.Params != null) row["params"] = item.Params;
                row["batch_id"] = item.BatchId;
                row["result_code"] = item.ResultCode;
                row["total_time"] = item.TotalTime;
                row["img_file_type"] = item.ImgFileType;
                row["create_date"] = item.CreateDate;
            }
            return row;
        }


        public override AlgResultMasterModel GetModel(DataRow item)
        {
            AlgResultMasterModel model = new AlgResultMasterModel
            {
                Id = item.Field<int>("id"),
                BatchId = item.Field<int?>("batch_id"),
                BatchCode = item.Field<string>("batch_code"),
                TId = item.Field<int?>("tid"),
                ImgFile = item.Field<string>("img_file"),
                ImgFileType = (AlgorithmResultType)item.Field<sbyte>("img_file_type"),
                TName = item.Field<string>("tname"),
                ResultCode = item.Field<int>("result_code"),
                TotalTime = item.Field<int>("total_time"),
                Result = item.Field<string>("result"),
                Params = item.Field<string>("params"),
                CreateDate = item.Field<DateTime>("create_date"),
            };

            return model;
        }
    }
}
