using ColorVision.Engine.MySql.ORM;
using System.Collections.Generic;
using System.Data;

namespace ColorVision.Engine.Services.SysDictionary
{
    public class SysDictionaryModel : PKModel
    {
        public string? Name { get; set; }
        public string? Code { get; set; }
        public int Type { get; set; }
        public int Pid { get; set; }
        public int Value { get; set; }
        public int TenantId { get; set; }
    }
    public class SysDictionaryDao : BaseDaoMaster<SysDictionaryModel>
    {
        public static SysDictionaryDao Instance { get; set; } = new SysDictionaryDao();

        public SysDictionaryDao() : base("v_scgd_sys_dictionary", "t_scgd_sys_dictionary", "id", true)
        {
        }

        public override SysDictionaryModel GetModelFromDataRow(DataRow item)
        {
            SysDictionaryModel model = new()
            {
                Id = item.Field<int>("id"),
                Name = item.Field<string>("name"),
                Code = item.Field<string>("code"),
                Pid = item.Field<int>("pid"),
                Value = item.Field<int>("val"),
                TenantId = item.Field<int>("tenant_id"),
            };
            return model;
        }

        public List<SysDictionaryModel> GetServiceTypes()
        {
            List<SysDictionaryModel> result = GetAllByPcode("service_type");
            return result;
        }

        public new List<SysDictionaryModel> GetAllByPcode(string pcode)
        {
            List<SysDictionaryModel> list = new();
            string sql = $"select * from {GetTableName()} where pcode='{pcode}'" + GetDelSQL(true);
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
