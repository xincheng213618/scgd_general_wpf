using System.Data;

namespace ColorVision.MySql.DAO
{
    public class AlgorithmFocusPointsResultModel : PKModel
    {
        public int BatchId { get; set; }

        public int ImgId { get; set; }
        public string? Value { get; set; }
        public string? ImgPointsX { get; set; }
        public string? ImgPointsY { get; set; }
        public bool Result { get; set; }    
    }


    public class AlgorithmFocusPointsResult : BaseDaoMaster<AlgorithmFocusPointsResultModel>
    {
        public AlgorithmFocusPointsResult() : base(string.Empty, "t_scgd_algorithm_focusPoints_result", "id", false)
        {
        }
        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add("id", typeof(int));
            dInfo.Columns.Add("batch_id", typeof(int));
            dInfo.Columns.Add("img_id", typeof(int));
            dInfo.Columns.Add("value", typeof(string));
            dInfo.Columns.Add("imgPoints_x", typeof(string));
            dInfo.Columns.Add("imgPoints_y", typeof(string));
            dInfo.Columns.Add("ret", typeof(bool));
            return dInfo;
        }


        public override AlgorithmFocusPointsResultModel GetModel(DataRow item)
        {
            AlgorithmFocusPointsResultModel model = new AlgorithmFocusPointsResultModel
            {
                Id = item.Field<int>("id"),
                BatchId = item.Field<int>("batch_id"),
                ImgId = item.Field<int>("img_id"),
                Value = item.Field<string>("value"),
                ImgPointsX = item.Field<string>("imgPoints_x"),
                ImgPointsY = item.Field<string>("imgPoints_y"),
                Result = item.Field<bool>("ret"),
            };
            return model;
        }

        public override DataRow Model2Row(AlgorithmFocusPointsResultModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["batch_id"] = item.BatchId;
                row["img_id"] = item.ImgId;
                row["value"] = item.Value;
                row["imgPoints_x"] = item.ImgPointsX;
                row["imgPoints_y"] = item.ImgPointsY;
                row["ret"] = item.Result;
            }
            return row;
        }
    }
}
