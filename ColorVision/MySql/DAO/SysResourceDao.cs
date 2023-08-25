using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.MySql.DAO
{
    public class SysResourceModel : PKModel
    {
        public SysResourceModel() { }
        public SysResourceModel(string name, string code, int tp, int pid, int tenantId)
        {
            this.Name = name;
            this.Code = code;
            this.TenantId = tenantId;
            this.Type = tp;
            this.Pid = pid;
            this.CreateDate = DateTime.Now;
        }

        public SysResourceModel(string name, string code, int tp, int tenantId)
        {
            this.Name = name;
            this.Code = code;
            this.TenantId = tenantId;
            this.Type = tp;
            this.CreateDate = DateTime.Now;
        }

        public string? Name { get; set; }
        public string? Code { get; set; }
        public string? TypeCode { get; set; }
        public int Type { get; set; }
        public int? Pid { get; set; }
        public string? Value { get; set; }
        public DateTime CreateDate { get; set; }
        public int TenantId { get; set; }
    }
    public class SysResourceDao : BaseDaoMaster<SysResourceModel>
    {
        public SysResourceDao() : base("v_scgd_sys_resource", "t_scgd_sys_resource", "id", true)
        {
        }

        public override SysResourceModel GetModel(DataRow item)
        {
            SysResourceModel model = new SysResourceModel
            {
                Id = item.Field<int>("id"),
                Name = item.Field<string>("name"),
                Code = item.Field<string>("code"),
                Type = item.Field<int>("type"),
                Pid = item.Field<int?>("pid"),
                TypeCode = item.Field<string>("type_code"),
                Value = item.Field<string>("txt_value"),
                CreateDate = item.Field<DateTime>("create_date"),
                TenantId = item.Field<int>("tenant_id"),
            };
            return model;
        }

        public override DataRow Model2Row(SysResourceModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                if (item.Name != null) row["name"] = item.Name;
                if (item.Code != null) row["code"] = item.Code;
                if (item.Pid != null) row["pid"] = item.Pid;
                if (item.Value != null) row["txt_value"] = item.Value;
                if (item.Type >= 0) row["type"] = item.Type;
                row["tenant_id"] = item.TenantId;
                row["create_date"] = item.CreateDate;
           }
            return row;
        }

        internal SysResourceModel? GetByCode(string code)
        {
            string sql = $"select * from {GetTableName()} where code=@code" + GetDelSQL(true);
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "code", code }
            };
            DataTable d_info = GetData(sql, param);
            return d_info.Rows.Count == 1 ? GetModel(d_info.Rows[0]) : default;
        }

        internal List<SysResourceModel> GetServices(int tenantId)
        {
            List<SysResourceModel> list = new List<SysResourceModel>();
            DataTable d_info = GetTablePidIsNullByPPcodeAndTenantId(tenantId);
            foreach (var item in d_info.AsEnumerable())
            {
                SysResourceModel? model = GetModel(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }

        public virtual DataTable GetTablePidIsNullByPPcodeAndTenantId(int tenantId)
        {
            string ppcode = "service_type";
            string sql = $"select * from {GetTableName()} where tenant_id={tenantId} and ( pid is null or pid=-1) and ppcode='{ppcode}'" + GetDelSQL(true);
            DataTable d_info = GetData(sql);
            return d_info;
        }

        internal int DeleteInCodes(string[] codes)
        {
            string sqlCode= string.Join(',', codes);
            string sql = $"update {TableName} set is_delete=1 where code in ('{sqlCode}')";
            return ExecuteNonQuery(sql);
        }
    }
}
