﻿using System.Collections.Generic;
using System.Data;
using ColorVision.MySql;

namespace ColorVision.Services.Dao
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

        public override SysDictionaryModModel GetModelFromDataRow(DataRow item)
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

        public SysDictionaryModModel GetByCode(string? code, int tenantId)
        {
            if (string.IsNullOrEmpty(code))
                return new SysDictionaryModModel();
            string sql = $"select * from {GetTableName()} where is_delete=0 and code=@code and tenant_id=@tenantId";
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "code", code },
                { "tenantId", tenantId }
            };
            DataTable d_info = GetData(sql, param);
            return d_info.Rows.Count == 1 ? GetModelFromDataRow(d_info.Rows[0]) : new SysDictionaryModModel();
        }
    }
}
