using System.Data;

namespace ColorVision.MySql.DAO
{
    public class AlgorithmLedCheckResultModel : PKModel
    { 
    
        public int BatchId { get; set; }

        public int ImgId { get; set; }
        public string? Value { get; set; }
        public string? Databanjin { get; set; }
        public string? DatazuobiaoX { get; set; }
        public string? DatazuobiaoY { get; set; }

        public string? PointX { get; set; }
        public string? PointY { get; set; }

        public string? LengthResult { get; set; }
        public bool Result { get; set; }    
    }


    public class AlgorithmLedCheckResult : BaseDaoMaster<AlgorithmLedCheckResultModel>
    {
        public AlgorithmLedCheckResult() : base(string.Empty, "t_scgd_algorithm_ledcheck_result", "id", false)
        {
        }
        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add("id", typeof(int));
            dInfo.Columns.Add("batch_id", typeof(int));
            dInfo.Columns.Add("img_id", typeof(int));
            dInfo.Columns.Add("value", typeof(string));
            dInfo.Columns.Add("databanjin", typeof(string));
            dInfo.Columns.Add("datazuobiaoX", typeof(string));
            dInfo.Columns.Add("datazuobiaoY", typeof(string));
            dInfo.Columns.Add("PointX", typeof(string));
            dInfo.Columns.Add("PointY", typeof(string));
            dInfo.Columns.Add("LengthResult", typeof(string));
            dInfo.Columns.Add("ret", typeof(bool));
            return dInfo;
        }


        public override AlgorithmLedCheckResultModel GetModel(DataRow item)
        {
            AlgorithmLedCheckResultModel model = new AlgorithmLedCheckResultModel
            {
                Id = item.Field<int>("id"),
                BatchId = item.Field<int>("batch_id"),
                ImgId = item.Field<int>("img_id"),
                Value = item.Field<string?>("value"),
                Databanjin = item.Field<string?>("databanjin"),
                DatazuobiaoX = item.Field<string?>("datazuobiaoX"),
                DatazuobiaoY = item.Field<string?>("datazuobiaoY"),
                PointX = item.Field<string?>("PointX"),
                PointY = item.Field<string?>("PointY"),
                LengthResult = item.Field<string?>("LengthResult"),
                Result = item.Field<bool>("ret"),
            };
            return model;
        }

        public override DataRow Model2Row(AlgorithmLedCheckResultModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["batch_id"] = item.BatchId;
                row["img_id"] = item.ImgId;
                row["value"] = item.Value;
                row["databanjin"] = item.Databanjin;
                row["datazuobiaoX"] = item.DatazuobiaoX;
                row["datazuobiaoY"] = item.DatazuobiaoY;
                row["PointX"] = item.PointX;
                row["PointY"] = item.PointY;
                row["LengthResult"] = item.LengthResult;
                row["ret"] = item.Result;
            }
            return row;
        }
    }
}
