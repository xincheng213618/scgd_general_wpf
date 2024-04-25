using System.Collections.Generic;
using System.Data;
using ColorVision.MySql;
using ColorVision.MySql.ORM;
using NPOI.SS.Formula.Functions;

namespace ColorVision.Services.Dao
{
    public class SysDictionaryModItemValidateModel : PKModel
    {
        public int? Pid { get; set; }
        public string? Code { get; set; }

        public float ValMax { get; set; }
        public float ValMin { get; set; }
        public string? ValEqual { get; set; }
        public short ValRadix { get; set; }
        public short ValType { get; set; }
        public bool IsEnable { get; set; }
    }
    public class SysDictionaryModItemValidateDao : BaseTableDao<SysDictionaryModItemValidateModel>
    {
        public static SysDictionaryModDao Instance { get; set; } = new SysDictionaryModDao();

        public SysDictionaryModItemValidateDao() : base("t_scgd_sys_dictionary_mod_item_validate", "id")
        {

        }

        public override SysDictionaryModItemValidateModel GetModelFromDataRow(DataRow item) => new SysDictionaryModItemValidateModel
        {
            Id = item.Field<int>("id"),
            Pid = item.Field<int>("pid"),
            Code = item.Field<string?>("code"),
            ValMax = item.Field<float>("val_max"),
            ValMin = item.Field<float>("val_min"),
            ValEqual = item.Field<string?>("val_equal"),
            ValRadix = item.Field<short>("val_radix"),
            ValType = item.Field<short>("val_type"),
            IsEnable = item.Field<bool>("is_enable"),
        };

        public void SetValue<T>(T t,T t1)
        {
            if (t != null)
                t1 = t;
        }

        public override DataRow Model2Row(SysDictionaryModItemValidateModel item, DataRow row)
        {
            if (item != null)
            {
                row["id"] = item.Id;
                row["pid"] = item.Pid;
                if (item.Code != null)
                    row["code"] = item.Code;
                row["val_max"] = item.ValMax;
                row["val_min"] = item.ValMin;
                if (item.ValEqual!=null)
                    row["val_equal"] = item.ValEqual;
                row["val_radix"] = item.ValRadix;
                row["val_type"] = item.ValType;
                row["is_enable"] = item.IsEnable;
            }
            return row;
        }

    }
}
