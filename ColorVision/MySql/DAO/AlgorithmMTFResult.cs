using ColorVision.Template;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.DAO
{
    public class AlgorithmMTFResultModel : PKModel
    {
        public int BatchId { get; set; }

        public int ImgId { get; set; }
        public string? Value { get; set; }

        public bool Result { get; set; }    
    }


    public class AlgorithmMTFResult : BaseDaoMaster<AlgorithmMTFResultModel>
    {
        public AlgorithmMTFResult() : base(string.Empty, "t_scgd_algorithm_mtf_result", "id", false)
        {
        }
        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add("id", typeof(int));
            dInfo.Columns.Add("batch_id", typeof(int));
            dInfo.Columns.Add("img_id", typeof(int));
            dInfo.Columns.Add("value", typeof(string));
            dInfo.Columns.Add("ret", typeof(bool));
            return dInfo;
        }


        public override AlgorithmMTFResultModel GetModel(DataRow item)
        {
            AlgorithmMTFResultModel model = new AlgorithmMTFResultModel
            {
                Id = item.Field<int>("id"),
                BatchId = item.Field<int>("batch_id"),
                ImgId = item.Field<int>("img_id"),
                Value = item.Field<string>("value"),
                Result = item.Field<bool>("ret"),
            };
            return model;
        }

        public override DataRow Model2Row(AlgorithmMTFResultModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["batch_id"] = item.BatchId;
                row["img_id"] = item.ImgId;
                row["value"] = item.Value;
                row["ret"] = item.Result;
            }
            return row;
        }
    }
}
