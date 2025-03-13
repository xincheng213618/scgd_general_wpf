using ColorVision.Engine.MySql.ORM;
using System.Data;

namespace ColorVision.Engine.Templates.FOV
{
    public class AlgResultFOVModel : PKModel
    {
        public int? Pid { get; set; }
        public int? Pattern { get; set; }
        public int? Type { get; set; }
        public double? Radio { get; set; }
        public double? CameraDegrees { get; set; }
        public double? Dist { get; set; }
        public int? Threshold { get; set; }
        public double? Degrees { get; set; }
    }


    public class AlgResultFOVDao : BaseTableDao<AlgResultFOVModel>
    {
        public static AlgResultFOVDao Instance { get; set; } = new AlgResultFOVDao();
        public AlgResultFOVDao() : base("t_scgd_algorithm_result_detail_fov", "id") { }

        public override AlgResultFOVModel GetModelFromDataRow(DataRow item)
        {
            AlgResultFOVModel model = new()
            {
                Id = item.Field<int>("id"),
                Pid = item.Field<int?>("pid") ?? -1,
                Pattern = item.Field<sbyte?>("pattern") ?? -1,
                Type = item.Field<sbyte?>("type"),
                Threshold = item.Field<int?>("threshold") ?? 0,
                Radio = item.Field<double?>("radio") ?? 0,
                CameraDegrees = item.Field<double?>("camera_degrees") ?? 0,
                Dist = item.Field<double?>("dist") ?? 0,
                Degrees = item.Field<double?>("degrees") ?? 0,
            };
            return model;
        }

        public override DataRow Model2Row(AlgResultFOVModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["pid"] = item.Pid;
                row["pattern"] = item.Pattern;
                row["type"] = item.Type;
                row["threshold"] = item.Threshold;
                row["radio"] = item.Radio;
                row["camera_degrees"] = item.CameraDegrees;
                row["dist"] = item.Dist;
                row["degrees"] = item.Degrees;
            }
            return row;
        }
    }
}
