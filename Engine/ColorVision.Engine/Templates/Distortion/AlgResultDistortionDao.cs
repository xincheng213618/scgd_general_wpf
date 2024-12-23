using ColorVision.Engine.MySql.ORM;
using MQTTMessageLib.Algorithm;
using System.Data;

namespace ColorVision.Engine.Templates.Distortion
{
    public class AlgResultDistortionModel : PKModel
    {
        [Column("pid")]
        public int? Pid { get; set; }
        [Column("type")]
        public DistortionType Type { get; set; }
        [Column("layout_type")]
        public DisLayoutType LayoutType { get; set; }
        [Column("slope_type")]
        public DisSlopeType SlopeType { get; set; }
        [Column("corner_type")]
        public DisCornerType CornerType { get; set; }
        [Column("point_x")]
        public double PointX { get; set; }
        [Column("point_y")]
        public double PointY { get; set; }
        [Column("max_ratio")]
        public double MaxRatio { get; set; }
        [Column("rotation_angle")]
        public double RotationAngle { get; set; }
        [Column("final_points")]
        public string? FinalPoints { get; set; }
    }


    public class AlgResultDistortionDao : BaseTableDao<AlgResultDistortionModel>
    {
        public static AlgResultDistortionDao Instance { get; set; } = new AlgResultDistortionDao();
        public AlgResultDistortionDao() : base("t_scgd_algorithm_result_detail_distortion", "id") { }

        public override AlgResultDistortionModel GetModelFromDataRow(DataRow item)
        {
            AlgResultDistortionModel model = new()
            {
                Id = item.Field<int>("id"),
                Pid = item.Field<int?>("pid") ?? -1,
                SlopeType = (DisSlopeType)item.Field<sbyte>("slope_type"),
                Type = (DistortionType)item.Field<sbyte>("type"),
                LayoutType = (DisLayoutType)item.Field<sbyte>("layout_type"),
                CornerType = (DisCornerType)item.Field<sbyte>("corner_type"),
                PointX = item.Field<double>("point_x"),
                PointY = item.Field<double>("point_y"),
                MaxRatio = item.Field<double>("max_ratio"),
                RotationAngle = item.Field<double>("rotation_angle"),
                FinalPoints = item.Field<string>("final_points"),
            };
            return model;
        }

        public override DataRow Model2Row(AlgResultDistortionModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["pid"] = item.Pid;
                row["slope_type"] = item.SlopeType;
                row["type"] = item.Type;
                row["layout_type"] = item.LayoutType;
                row["corner_type"] = item.CornerType;
                row["point_x"] = item.PointX;
                row["point_y"] = item.PointY;
                row["max_ratio"] = item.MaxRatio;
                row["rotation_angle"] = item.RotationAngle;
                row["final_points"] = item.FinalPoints;
            }
            return row;
        }
    }
}
