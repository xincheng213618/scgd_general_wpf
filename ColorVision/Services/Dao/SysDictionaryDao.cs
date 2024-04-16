using System.Data;
using ColorVision.MySql;

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
            };
            return model;
        }
    }
}
