using System.Data;
using ColorVision.MySql;

namespace ColorVision.Services.Dao
{
    public class CameraTempModel : PKModel
    {
        public string? SnID { get; set; }
        public string? Value { get; set; }
    }

    public class CameraTempDao : BaseDaoMaster<CameraTempModel>
    {
        public CameraTempDao() : base(string.Empty, "t_scgd_camera_temp", "id", true)
        {
        }
        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add("id", typeof(int));
            dInfo.Columns.Add("SnID", typeof(string));
            dInfo.Columns.Add("Value", typeof(string));
            return dInfo;
        }

        public override CameraTempModel GetModel(DataRow item)
        {
            CameraTempModel model = new CameraTempModel
            {
                Id = item.Field<int>("id"),
                SnID = item.Field<string>("SnID"),
                Value = item.Field<string>("Value")
            };
            return model;
        }

        public override DataRow Model2Row(CameraTempModel item, DataRow row)
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
