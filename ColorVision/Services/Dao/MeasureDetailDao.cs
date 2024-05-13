using System.Data;
using ColorVision.MySql;
using ColorVision.MySql.ORM;

namespace ColorVision.Services.Dao
{
    public class MeasureDetailModel : PKModel
    {
        public string? Name { get; set; }
        public string? PCode { get; set; }
        public string? PName { get; set; }
        public int? TID { get; set; }
        public int TType { get; set; }
        public int Pid { get; set; }
        public bool IsEnable { get; set; } = true;
        public bool IsDelete { get; set; }
    }
    public class MeasureDetailDao : BaseDaoMaster<MeasureDetailModel>
    {
        public MeasureDetailDao() : base("v_scgd_measure_template_detail", "t_scgd_measure_template_detail", "id", true)
        {
        }

        public override MeasureDetailModel GetModelFromDataRow(DataRow item)
        {
            MeasureDetailModel model = new()
            {
                Id = item.Field<int>("id"),
                TID = item.Field<int>("t_id"),
                TType = item.Field<sbyte>("t_type"),
                Pid = item.Field<int>("pid"),
                Name = item.Field<string>("name"),
                PCode = item.Field<string>("pcode"),
                PName = item.Field<string>("pname"),
                IsEnable = item.Field<sbyte>("is_enable") == 1 ? true : false,
                IsDelete = item.Field<sbyte>("is_delete") == 1 ? true : false,
            };

            return model;
        }

        public override DataRow Model2Row(MeasureDetailModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["t_id"] = item.TID;
                row["t_type"] = item.TType;
                row["pid"] = item.Pid;
                row["is_enable"] = item.IsEnable;
                row["is_delete"] = item.IsDelete;
            }
            return row;
        }
    }
}
