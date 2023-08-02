using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.DAO
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

        public override MeasureDetailModel GetModel(DataRow item)
        {
            MeasureDetailModel model = new MeasureDetailModel
            {
                Id = item.Field<int>("id"),
                TID = item.Field<int>("t_id"),
                TType = item.Field<SByte>("t_type"),
                Pid = item.Field<int>("pid"),
                Name = item.Field<string>("name"),
                PCode = item.Field<string>("pcode"),
                PName = item.Field<string>("pname"),
                IsEnable = item.Field<SByte>("is_enable") == 1 ? true : false,
                IsDelete = item.Field<SByte>("is_delete") == 1 ? true : false,
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
