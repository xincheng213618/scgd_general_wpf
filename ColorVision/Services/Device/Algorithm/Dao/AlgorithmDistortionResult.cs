using System.Data;
using ColorVision.MySql;

namespace ColorVision.Services.Device.Algorithm.Dao
{
    public class AlgorithmDistortionResultModel : PKModel
    {
        public int BatchId { get; set; }

        public int ImgId { get; set; }
        public string? Value { get; set; }

        public string? FinalPointsX { get; set; }
        public string? FinalPointsY { get; set; }



        public double PointX { get; set; }
        public double PointY { get; set; }
        public double MaxErrorRatio { get; set; }
        public double T { get; set; }
        public bool Result { get; set; }
    }

    public class AlgorithmDistortionResult : BaseDaoMaster<AlgorithmDistortionResultModel>
    {
        public AlgorithmDistortionResult() : base(string.Empty, "t_scgd_algorithm_distortion_result", "id", false)
        {

        }
        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add("id", typeof(int));
            dInfo.Columns.Add("batch_id", typeof(int));
            dInfo.Columns.Add("img_id", typeof(int));
            dInfo.Columns.Add("finalPointsX", typeof(double));
            dInfo.Columns.Add("finalPointsY", typeof(double));
            dInfo.Columns.Add("pointx", typeof(double));
            dInfo.Columns.Add("pointy", typeof(double));
            dInfo.Columns.Add("maxErrorRatio", typeof(double));
            dInfo.Columns.Add("t", typeof(double));
            dInfo.Columns.Add("value", typeof(string));
            dInfo.Columns.Add("ret", typeof(bool));
            return dInfo;
        }


        public override AlgorithmDistortionResultModel GetModel(DataRow item)
        {
            AlgorithmDistortionResultModel model = new AlgorithmDistortionResultModel
            {
                Id = item.Field<int>("id"),
                BatchId = item.Field<int?>("batch_id") ?? -1,
                ImgId = item.Field<int?>("img_id") ?? -1,
                Value = item.Field<string?>("value"),
                FinalPointsX = item.Field<string?>("finalPointsX"),
                FinalPointsY = item.Field<string?>("finalPointsY"),
                PointX = item.Field<double?>("pointx") ?? 0,
                PointY = item.Field<double?>("pointy") ?? 0,
                MaxErrorRatio = item.Field<double?>("maxErrorRatio") ?? 0,
                T = item.Field<double?>("t") ?? 0,
                Result = item.Field<bool?>("ret") ?? false,
            };
            return model;
        }

        public override DataRow Model2Row(AlgorithmDistortionResultModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["batch_id"] = item.BatchId;
                row["img_id"] = item.ImgId;
                row["value"] = item.Value;
                row["pointx"] = item.PointX;
                row["pointy"] = item.PointY;
                row["finalPointsX"] = item.FinalPointsX;
                row["finalPointsY"] = item.FinalPointsY;
                row["maxErrorRatio"] = item.MaxErrorRatio;
                row["t"] = item.T;
                row["ret"] = item.Result;
            }
            return row;
        }
    }
}
