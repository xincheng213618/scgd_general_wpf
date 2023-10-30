using System.Data;

namespace ColorVision.MySql.DAO
{
    public class AlgorithmDistortionResultModel : PKModel
    {
        public int BatchId { get; set; }

        public int ImgId { get; set; }
        public string? Value { get; set; }

        public double PointX { get; set; }
        public double PointY { get; set; }
        public double MaxErrorRatio { get; set; }
        public double T { get; set; }
        public string? ISize { get; set; }
        public string? TBlobThreParams { get; set; }
        public int TimeoutNumLimit { get; set; }
        public string? DType { get; set; }
        public string? IType { get; set; }
        public string? Type { get; set; }
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
            dInfo.Columns.Add("pointx", typeof(double));
            dInfo.Columns.Add("pointy", typeof(double));
            dInfo.Columns.Add("maxErrorRatio", typeof(double));
            dInfo.Columns.Add("t", typeof(double));
            dInfo.Columns.Add("value", typeof(string));
            dInfo.Columns.Add("iSize", typeof(string));
            dInfo.Columns.Add("tBlobThreParams", typeof(string));
            dInfo.Columns.Add("timeoutNumLimit", typeof(int));
            dInfo.Columns.Add("dType", typeof(string));
            dInfo.Columns.Add("lType", typeof(string));
            dInfo.Columns.Add("type", typeof(string));
            dInfo.Columns.Add("ret", typeof(bool));
            return dInfo;
        }


        public override AlgorithmDistortionResultModel GetModel(DataRow item)
        {
            AlgorithmDistortionResultModel model = new AlgorithmDistortionResultModel
            {
                Id = item.Field<int>("id"),
                BatchId = item.Field<int>("batch_id"),
                ImgId = item.Field<int>("img_id"),
                Value = item.Field<string>("value"),
                PointX = item.Field<double>("pointx"),
                PointY = item.Field<double>("pointy"),
                MaxErrorRatio = item.Field<double>("maxErrorRatio"),
                T = item.Field<double>("t"),
                ISize = item.Field<string>("iSize"),
                TBlobThreParams = item.Field<string>("tBlobThreParams"),
                TimeoutNumLimit = item.Field<int>("timeoutNumLimit"),
                DType = item.Field<string>("dType"),
                IType = item.Field<string>("lType"),
                Type = item.Field<string>("type"),
                Result = item.Field<bool>("ret"),
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
                row["maxErrorRatio"] = item.MaxErrorRatio;
                row["t"] = item.T;
                row["iSize"] = item.ISize;
                row["tBlobThreParams"] = item.TBlobThreParams;
                row["timeoutNumLimit"] = item.TimeoutNumLimit;
                row["dType"] = item.DType;
                row["lType"] = item.IType;
                row["type"] = item.Type;
                row["ret"] = item.Result;
            }
            return row;
        }
    }
}
