using System.Data;

namespace ColorVision.MySql.DAO
{
    public class AlgorithmSfrResultModel : PKModel
    {
        public int BatchId { get; set; }

        public int ImgId { get; set; }
        public string? Value { get; set; }

        public string? Pdfrequency { get; set; }

        public string? PdomainSamplingData { get; set; }

        public bool Result { get; set; }    
    }


    public class AlgorithmSfrResult : BaseDaoMaster<AlgorithmSfrResultModel>
    {
        public AlgorithmSfrResult() : base(string.Empty, "t_scgd_algorithm_sfr_result", "id", false)
        {
        }
        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add("id", typeof(int));
            dInfo.Columns.Add("batch_id", typeof(int));
            dInfo.Columns.Add("img_id", typeof(int));
            dInfo.Columns.Add("value", typeof(string));
            dInfo.Columns.Add("pdfrequency", typeof(string));
            dInfo.Columns.Add("pdomainSamplingData", typeof(string));
            dInfo.Columns.Add("ret", typeof(bool));
            return dInfo;
        }


        public override AlgorithmSfrResultModel GetModel(DataRow item)
        {
            AlgorithmSfrResultModel model = new AlgorithmSfrResultModel
            {
                Id = item.Field<int>("id"),
                BatchId = item.Field<int>("batch_id"),
                ImgId = item.Field<int>("img_id"),
                Value = item.Field<string>("value"),
                Pdfrequency = item.Field<string>("pdfrequency"),
                PdomainSamplingData = item.Field<string>("pdomainSamplingData"),
                Result = item.Field<bool>("ret"),
            };
            return model;
        }

        public override DataRow Model2Row(AlgorithmSfrResultModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["batch_id"] = item.BatchId;
                row["img_id"] = item.ImgId;
                row["value"] = item.Value;
                row["pdfrequency"] = item.Pdfrequency;
                row["pdomainSamplingData"] = item.PdomainSamplingData;
                row["ret"] = item.Result;
            }
            return row;
        }
    }
}
