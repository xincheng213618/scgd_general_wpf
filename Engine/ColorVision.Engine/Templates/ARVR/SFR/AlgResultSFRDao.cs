using ColorVision.Engine.MySql.ORM;
using System.Data;

namespace ColorVision.Engine.Templates.SFR
{
    public class AlgResultSFRModel : PKModel
    {
        public int? Pid { get; set; }
        public int? RoiX { get; set; }
        public int? RoiY { get; set; }
        public int? RoiWidth { get; set; }
        public int? RoiHeight { get; set; }
        public double? Gamma { get; set; }
        public string? Pdfrequency { get; set; }
        public string? PdomainSamplingData { get; set; }
    }

    public class AlgResultSFRDao : BaseTableDao<AlgResultSFRModel>
    {
        public static AlgResultSFRDao Instance { get; set; } = new AlgResultSFRDao();

        public AlgResultSFRDao() : base("t_scgd_algorithm_result_detail_sfr", "id")
        {
        }

        public override AlgResultSFRModel GetModelFromDataRow(DataRow item)
        {
            AlgResultSFRModel model = new()
            {
                Id = item.Field<int>("id"),
                Pid = item.Field<int?>("pid") ?? -1,
                RoiX = item.Field<int?>("roi_x") ?? -1,
                RoiY = item.Field<int?>("roi_y"),
                RoiWidth = item.Field<int?>("roi_width") ?? 0,
                RoiHeight = item.Field<int?>("roi_height") ?? 0,
                Gamma = item.Field<double?>("gamma") ?? 0,
                Pdfrequency = item.Field<string>("pdfrequency"),
                PdomainSamplingData = item.Field<string>("pdomain_sampling_data"),
            };
            return model;
        }

        public override DataRow Model2Row(AlgResultSFRModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["pid"] = item.Pid;
                row["roi_x"] = item.RoiX;
                row["roi_y"] = item.RoiY;
                row["roi_width"] = item.RoiWidth;
                row["roi_height"] = item.RoiHeight;
                row["gamma"] = item.Gamma;
                row["pdfrequency"] = item.Pdfrequency;
                row["pdomain_sampling_data"] = item.PdomainSamplingData;
            }
            return row;
        }
    }
}
