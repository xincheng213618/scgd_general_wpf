#pragma warning disable CS8601,CS8603
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Dao;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    public class AlgResultMasterModel : PKModel
    {
        public AlgResultMasterModel() { }

        [Column("tid")]
        public int? TId { get; set; }

        [Column("tname")]
        public string TName { get; set; }

        [Column("img_file")]
        public string ImgFile { get; set; }

        [Column("img_file_type")]
        public AlgorithmResultType ImgFileType { get; set; }

        [Column("version")]
        public string version { get; set; }

        [Column("batch_id")]
        public int? BatchId { get; set; }

        public string BatchCode { get => BatchResultMasterDao.Instance.GetById(BatchId)?.Code; }

        [Column("params")]
        public string Params { get; set; }

        [Column("result_code")]
        public int? ResultCode { get; set; }

        [Column("result")]
        public string Result { get; set; }

        [Column("img_result")]
        public string ResultImagFile { get; set; }

        [Column("total_time")]
        public long TotalTime { get; set; }
        [Column("create_date")]
        public DateTime? CreateDate { get; set; }

    }


    public class AlgResultMasterDao : BaseTableDao<AlgResultMasterModel>
    {
        public static AlgResultMasterDao Instance { get; set; } = new AlgResultMasterDao();

        public AlgResultMasterDao() : base("t_scgd_algorithm_result_master", "id")
        {
        }


        public List<AlgResultMasterModel> ConditionalQuery(string id, string batchid, string ImageType, string fileName, DateTime? dateTimeStart, DateTime? dateTimeEnd,int limit)
        {
            Dictionary<string, object> keyValuePairs = new(0);
            keyValuePairs.Add("id", id);
            keyValuePairs.Add("batch_id", batchid);
            keyValuePairs.Add("img_file_type", ImageType);
            keyValuePairs.Add("img_file", fileName);
#pragma warning disable CS8604 // 引用类型参数可能为 null。
            keyValuePairs.Add(">create_date", dateTimeStart);
            keyValuePairs.Add("<create_date", dateTimeEnd);
#pragma warning restore CS8604 // 引用类型参数可能为 null。
            return ConditionalQuery(keyValuePairs,limit);
        }


    }
}
