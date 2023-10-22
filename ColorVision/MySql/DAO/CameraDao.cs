using System.Data;

namespace ColorVision.MySql.DAO
{
    public class CameraModel : PKModel
    {
        public string? SnID { get; set; }
        public string? Value { get; set; }
    }

    public class CameraDao : BaseDaoMaster<CameraModel>
    {
        public CameraDao() : base(string.Empty, "t_scgd_sys_camera", "id", true)
        {
        }
        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add("id", typeof(int));
            dInfo.Columns.Add("SnID", typeof(string));
            dInfo.Columns.Add("Value", typeof(string));
            return dInfo;
        }

        public override CameraModel GetModel(DataRow item)
        {
            CameraModel model = new CameraModel
            {
                Id = item.Field<int>("id"),
                SnID = item.Field<string>("SnID"),
                Value = item.Field<string>("Value")
            };
            return model;
        }

        public override DataRow Model2Row(CameraModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["SnID"] = item.SnID;
                row["Value"] = item.Value;
            }
            return row;
        }
    }




}
