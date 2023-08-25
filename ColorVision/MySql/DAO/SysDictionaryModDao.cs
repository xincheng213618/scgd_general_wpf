using System;
using System.Collections.Generic;
using System.Data;

namespace ColorVision.MySql.DAO
{
    public class SysDictionaryModModel : PKModel
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public int TenantId { get; set; }
    }
    public class SysDictionaryModDao : BaseDaoMaster<SysDictionaryModModel>
    {
        public SysDictionaryModDao() : base(string.Empty, "t_scgd_sys_dictionary_mod_master", "id", true)
        {
        }

        public override SysDictionaryModModel GetModel(DataRow item)
        {
            SysDictionaryModModel model = new SysDictionaryModModel
            {
                Id = item.Field<int>("id"),
                Code = item.Field<string>("code"),
                Name = item.Field<string>("name"),
                TenantId = item.Field<int>("tenant_id"),
            };

            return model;
        }

        public SysDictionaryModModel GetByCode(string? code,int tenantId)
        {
            if (String.IsNullOrEmpty(code))
                return new SysDictionaryModModel();
            string sql = $"select * from {GetTableName()} where is_delete=0 and code=@code and tenant_id=@tenantId";
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "code", code },
                { "tenantId", tenantId }
            };
            DataTable d_info = GetData(sql, param);
            return d_info.Rows.Count == 1 ? GetModel(d_info.Rows[0]) : new SysDictionaryModModel();
        }
    }
}
