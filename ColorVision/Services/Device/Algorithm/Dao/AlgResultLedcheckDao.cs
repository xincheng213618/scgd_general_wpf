using System.Data;
using ColorVision.MySql;

namespace ColorVision.Services.Device.Algorithm.Dao
{
    public class AlgResultLedcheckModel : PKModel
    {
        public int? Pid { get; set; }
        public int? PosX { get; set; }
        public int? PosY { get; set; }
        public float? Radius { get; set; }
    }
    public class AlgResultLedcheckDao : BaseDaoMaster<AlgResultLedcheckModel>
    {
        public AlgResultLedcheckDao() : base(string.Empty, "t_scgd_algorithm_result_detail_ledcheck", "id", false)
        {
        }

        public override AlgResultLedcheckModel GetModel(DataRow item)
        {
            AlgResultLedcheckModel model = new AlgResultLedcheckModel
            {
                Id = item.Field<int>("id"),
                Pid = item.Field<int?>("pid") ?? -1,
                PosX = item.Field<int?>("pos_x") ?? -1,
                PosY = item.Field<int?>("pos_y"),
                Radius = item.Field<float?>("radius") ?? 0,
            };
            return model;
        }

        public override DataRow Model2Row(AlgResultLedcheckModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["pid"] = item.Pid;
                row["pos_x"] = item.PosX;
                row["pos_y"] = item.PosY;
                row["radius"] = item.Radius;
            }
            return row;
        }
    }
}
