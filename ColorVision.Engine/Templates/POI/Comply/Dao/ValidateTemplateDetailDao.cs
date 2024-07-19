#pragma warning disable CS8601
using ColorVision.Engine.MySql.ORM;
using System.Data;

namespace ColorVision.Engine.Templates.POI.Comply.Dao
{
    public class ValidateTemplateDetailModel : PKModel
    {
        public int DicPid { get; set; }

        public int Pid { get; set; }

        public string Code { get; set; }

        public float ValMax { get; set; }
        public float ValMin { get; set; }
        public string ValEqual { get; set; }
        public short ValRadix { get; set; }
        public short ValType { get; set; }
    }



    public class ValidateTemplateDetailDao : BaseTableDao<ValidateTemplateDetailModel>
    {
        public static ValidateTemplateDetailDao Instance { get; set; } = new ValidateTemplateDetailDao();

        public ValidateTemplateDetailDao() : base("t_scgd_rule_validate_template_detail", "id")
        {
        }

        public override ValidateTemplateDetailModel GetModelFromDataRow(DataRow item)
        {
            ValidateTemplateDetailModel model = new()
            {
                Id = item.Field<int>("id"),
                DicPid = item.Field<int>("dic_pid"),
                Pid = item.Field<int>("pid"),
                Code = item.Field<string>("code"),
                ValMax = item.Field<float>("val_max"),
                ValMin = item.Field<float>("val_min"),
                ValEqual = item.Field<string>("val_equal"),
                ValRadix = item.Field<short>("val_radix"),
                ValType = item.Field<short>("val_type"),
            };

            return model;
        }

        public override DataRow Model2Row(ValidateTemplateDetailModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["dic_pid"] = item.DicPid;
                row["pid"] = item.Pid;
                if (item.Code != null)
                    row["code"] = item.Code;
                row["val_max"] = item.ValMax;
                row["val_min"] = item.ValMin;
                if (item.ValEqual != null)
                    row["val_equal"] = item.ValEqual;
                row["val_radix"] = item.ValRadix;
                row["val_type"] = item.ValType;
            }
            return row;
        }


    }
}
