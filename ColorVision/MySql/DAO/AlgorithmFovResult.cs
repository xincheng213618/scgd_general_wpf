using System.Data;

namespace ColorVision.MySql.DAO
{
    public class AlgorithmFovResultModel : PKModel
    {
        public int BatchId { get; set; }
        public int ImgId { get; set; }
        public string? Value { get; set; }
        public float Coordinates1 { get; set; }
        public float Coordinates2 { get; set; }
        public float Coordinates3 { get; set; }
        public float Coordinates4 { get; set; }

        public double FovDegrees { get; set; }

        public bool Result { get; set; }    
    }


    public class AlgorithmFOVResult : BaseDaoMaster<AlgorithmFovResultModel>
    {
        public AlgorithmFOVResult() : base(string.Empty, "t_scgd_algorithm_fov_result", "id", false)
        {
        }
        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add("id", typeof(int));
            dInfo.Columns.Add("batch_id", typeof(int));
            dInfo.Columns.Add("img_id", typeof(int));
            dInfo.Columns.Add("value", typeof(string));
            dInfo.Columns.Add("coordinates1", typeof(float));
            dInfo.Columns.Add("coordinates2", typeof(float));
            dInfo.Columns.Add("coordinates3", typeof(float));
            dInfo.Columns.Add("coordinates4", typeof(float));
            dInfo.Columns.Add("fovDegrees", typeof(double));
            dInfo.Columns.Add("ret", typeof(bool));
            return dInfo;
        }


        public override AlgorithmFovResultModel GetModel(DataRow item)
        {
            AlgorithmFovResultModel model = new AlgorithmFovResultModel
            {
                Id = item.Field<int>("id"),
                BatchId = item.Field<int?>("batch_id") ?? -1,
                ImgId = item.Field<int?>("img_id") ?? -1,
                Value = item.Field<string?>("value"),
                Coordinates1 = item.Field<float?>("coordinates1") ?? 0,
                Coordinates2 = item.Field<float?>("coordinates2") ?? 0,
                Coordinates3 = item.Field<float?>("coordinates3") ?? 0,
                Coordinates4 = item.Field<float?>("coordinates4") ?? 0,
                FovDegrees = item.Field<double?>("fovDegrees") ?? 0,
                Result = item.Field<bool?>("ret")??false,
            };
            return model;
        }

        public override DataRow Model2Row(AlgorithmFovResultModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["batch_id"] = item.BatchId;
                row["img_id"] = item.ImgId;
                row["value"] = item.Value;
                row["coordinates1"] = item.Coordinates1;
                row["coordinates2"] = item.Coordinates2;
                row["coordinates3"] = item.Coordinates3;
                row["coordinates4"] = item.Coordinates4;
                row["fovDegrees"] = item.FovDegrees;
                row["ret"] = item.Result;
            }
            return row;
        }
    }
}
