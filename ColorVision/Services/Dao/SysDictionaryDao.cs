using System.Collections.Generic;
using System.Data;
using ColorVision.MySql;
using ColorVision.MySql.ORM;

namespace ColorVision.Services.Dao
{
    public class SysDictionaryModel : PKModel
    {
        public string? Name { get; set; }
        public string? Code { get; set; }
        public int Type { get; set; }
        public int Pid { get; set; }
        public int Value { get; set; }
        public int TenantId { get; set; }
        public bool IsHide { get; set; }
    }
    public class SysDictionaryDao : BaseDaoMaster<SysDictionaryModel>
    {
        public static SysDictionaryDao Instance { get; set; } = new SysDictionaryDao();

        public SysDictionaryDao() : base("v_scgd_sys_dictionary", "t_scgd_sys_dictionary", "id", true)
        {
        }

        public override SysDictionaryModel GetModelFromDataRow(DataRow item)
        {
            SysDictionaryModel model = new SysDictionaryModel
            {
                Id = item.Field<int>("id"),
                Name = item.Field<string>("name"),
                Code = item.Field<string>("code"),
                Pid = item.Field<int>("pid"),
                Value = item.Field<int>("val"),
                TenantId = item.Field<int>("tenant_id"),
                IsHide = item.Field<bool>("is_hide"),
            };
            return model;
        }

        public List<SysDictionaryModel> GetServiceTypes()
        {
            List<SysDictionaryModel> result = this.GetAllByPcode("service_type");
            return result;
        }

        public new List<SysDictionaryModel> GetAllByPcode(string pcode)
        {
            List<SysDictionaryModel> list = new List<SysDictionaryModel>();
            string sql = $"select * from {GetTableName()} where is_hide=0 and pcode='{pcode}'" + GetDelSQL(true);
            DataTable d_info = GetData(sql);
            foreach (var item in d_info.AsEnumerable())
            {
                SysDictionaryModel? model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }
    }
}
