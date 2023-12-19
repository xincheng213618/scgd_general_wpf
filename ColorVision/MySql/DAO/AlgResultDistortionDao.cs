using MQTTMessageLib.Algorithm;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.DAO
{
    public class AlgResultDistortionModel : PKModel
    {
        public int? Pid { get; set; }

        public DistortionType Type { get; set; }
        public DisLayoutType LayoutType { get; set; }
        public DisSlopeType SlopeType { get; set; }
        public DisCornerType CornerType { get; set; }
        public double PointX { get; set; }
        public double PointY { get; set; }
        public double MaxRatio { get; set; }
        public double RotationAngle { get; set; }
        public string FinalPoints { get; set; }
    }
    public class AlgResultDistortionDao : BaseDaoMaster<AlgResultDistortionModel>
    {
        public AlgResultDistortionDao() : base(string.Empty, "t_scgd_algorithm_result_detail_distortion", "id", false)
        {
        }

        public override AlgResultDistortionModel GetModel(DataRow item)
        {
            AlgResultDistortionModel model = new AlgResultDistortionModel
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
