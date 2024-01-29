using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ColorVision.MySql;
using Mysqlx.Crud;
using ScottPlot.Drawing.Colormaps;

namespace ColorVision.Services.Dao
{
    public class SysResourceModel : PKModel
    {
        public SysResourceModel() { }
        public SysResourceModel(string name, string code, int tp, int pid, int tenantId)
        {
            Name = name;
            Code = code;
            TenantId = tenantId;
            Type = tp;
            Pid = pid;
            CreateDate = DateTime.Now;
        }

        public SysResourceModel(string name, string code, int tp, int tenantId)
        {
            Name = name;
            Code = code;
            TenantId = tenantId;
            Type = tp;
            CreateDate = DateTime.Now;
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
        public SysResourceDao() : base(string.Empty, "t_scgd_sys_resource", "id", true)
        {

        }

        public override SysResourceModel GetModelFromDataRow(DataRow item)
        {
            SysResourceModel model = new SysResourceModel
            {
                Id = item.Field<int>("id"),
                Name = item.Field<string>("name"),
                Code = item.Field<string>("code"),
                Type = item.Field<int>("type"),
                Pid = item.Field<int?>("pid"),
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

        public void ADDGroup(int groupId ,int resourceId)
        {
            string sql = "INSERT INTO t_scgd_sys_resource_group (resource_id, group_id) VALUES (@resourceId, @groupId)";
            var parameters = new Dictionary<string, object>();
            parameters.Add("@groupId", groupId);
            parameters.Add("@resourceId", resourceId);
            ExecuteNonQuery(sql, parameters);
        }

        public List<SysResourceModel> GetGroupResourceItems(int groupId)
        {
            List<SysResourceModel> list = new List<SysResourceModel>();

            string sql = "SELECT rg.group_id, r.id, r.name, r.code, r.type , r.pid, r.txt_value, r.create_date, r.tenant_id FROM t_scgd_sys_resource_group rg JOIN t_scgd_sys_resource r ON rg.resource_id = r.id WHERE  rg.group_id =@groupId";
            var parameters = new Dictionary<string, object>();
            parameters.Add("@groupId", groupId);
            var dInfo = GetData(sql, parameters);
            foreach (DataRow item in dInfo.Rows)
            {
                SysResourceModel? model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }

        public void DeleteGroupRelate(int groupId)
        {
            string sql = "DELETE FROM t_scgd_sys_resource_group WHERE group_id = @groupId";
            var parameters = new Dictionary<string, object>();
                parameters.Add("@groupId", groupId);
            ExecuteNonQuery(sql, parameters);
        }



        public List<SysResourceModel> GetAllResources(int tenantId = -1)
        {
            List<SysResourceModel> list = new List<SysResourceModel>();

            DataTable dInfo;
            string sql;

            sql = $"SELECT id, name, code,pid,txt_value,type,tenant_id,create_date FROM {GetTableName()} where 1=1 {(tenantId != 1 ? "and tenantId=@tenantId" : "")} and is_delete = 0 and is_enable = 1";
            var parameters = new Dictionary<string, object>();
            if (tenantId != -1)
                parameters.Add("@tenantId", tenantId);


            dInfo = GetData(sql, parameters);
            foreach (DataRow item in dInfo.Rows)
            {
                SysResourceModel? model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }



        internal SysResourceModel? GetByCode(string code)
        {
            string sql = $"select * from {GetTableName()} where code=@code" + GetDelSQL(true);
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "code", code }
            };
            DataTable d_info = GetData(sql, param);
            return d_info.Rows.Count == 1 ? GetModelFromDataRow(d_info.Rows[0]) : default;
        }

        internal List<SysResourceModel> GetServices(int tenantId)
        {
            List<SysResourceModel> list = new List<SysResourceModel>();
            DataTable d_info = GetTablePidIsNullByPPcodeAndTenantId(tenantId);
            foreach (var item in d_info.AsEnumerable())
            {
                SysResourceModel? model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }

        public void CreatResourceGroup()
        {
            string sql = "CREATE TABLE IF NOT EXISTS `t_scgd_sys_resource_group` ( `id` INT(11) NOT NULL AUTO_INCREMENT, `resource_id` INT(11) NOT NULL, `group_id` INT(11) NOT NULL, PRIMARY KEY (`id`), UNIQUE KEY `resource_group_unique` (`resource_id`, `group_id`), CONSTRAINT `fk_resource_id` FOREIGN KEY (`resource_id`) REFERENCES `t_scgd_sys_resource` (`id`) ON DELETE CASCADE ON UPDATE CASCADE, CONSTRAINT `fk_group_id` FOREIGN KEY (`group_id`) REFERENCES `t_scgd_sys_resource` (`id`) ON DELETE CASCADE ON UPDATE CASCADE ) ENGINE = INNODB CHARSET = utf8mb4;";
             ExecuteNonQuery(sql);
        }


        public List<SysResourceModel> GetResourceItems(int pid, int tenantId = -1)
        {
            List<SysResourceModel> list = new List<SysResourceModel>();

            string sql = $"SELECT id, name, code,pid,txt_value,type,tenant_id,create_date FROM {TableName} where 1=1 {(tenantId != 1 ? "and tenant_id=@tenantId" : "")} and pid=@pid and is_delete = 0 and is_enable = 1";
            var parameters = new Dictionary<string, object>();
            if (tenantId != -1)
                parameters.Add("@tenantId", tenantId);
            parameters.Add("@pid", pid);
            var dInfo = GetData(sql, parameters);
            foreach (DataRow item in dInfo.Rows)
            {
                SysResourceModel? model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }

        internal List<SysResourceModel> GetAllType(int type)
        {
            List<SysResourceModel> list = new List<SysResourceModel>();
            DataTable d_info = GetTableAllByType(type);
            foreach (var item in d_info.AsEnumerable())
            {
                SysResourceModel? model = GetModelFromDataRow(item);
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
            string sqlCode = string.Join(',', codes);
            string sql = $"update {TableName} set is_delete=1 where code in ('{sqlCode}')";
            return ExecuteNonQuery(sql);
        }
    }




    public class VSysResourceDao : BaseDaoMaster<SysResourceModel>
    {
        public VSysResourceDao() : base("v_scgd_sys_resource", "t_scgd_sys_resource", "id", true)
        {

        }

        public override SysResourceModel GetModelFromDataRow(DataRow item)
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


        public List<SysResourceModel> GetAllResources(int tenantId =-1)
        {
            List<SysResourceModel> list = new List<SysResourceModel>();

            DataTable dInfo;
            string sql;

            sql = $"SELECT id, name, code,pid,txt_value,type,tenant_id,create_date FROM {GetTableName()} where 1=1 {(tenantId!=1?"and tenantId=@tenantId":"")} and is_delete = 0 and is_enable = 1";
            var parameters = new Dictionary<string, object>();
            if (tenantId != -1)
                parameters.Add("@tenantId", tenantId);


            dInfo = GetData(sql, parameters);
            foreach (DataRow item in dInfo.Rows)
            {
                SysResourceModel? model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }



        internal SysResourceModel? GetByCode(string code)
        {
            string sql = $"select * from {GetTableName()} where code=@code" + GetDelSQL(true);
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "code", code }
            };
            DataTable d_info = GetData(sql, param);
            return d_info.Rows.Count == 1 ? GetModelFromDataRow(d_info.Rows[0]) : default;
        }




        internal List<SysResourceModel> GetServices(int tenantId)
        {
            List<SysResourceModel> list = new List<SysResourceModel>();
            DataTable d_info = GetTablePidIsNullByPPcodeAndTenantId(tenantId);
            foreach (var item in d_info.AsEnumerable())
            {
                SysResourceModel? model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }






        public List<SysResourceModel> GetResourceItems(int pid, int tenantId=-1)
        {
            List<SysResourceModel> list = new List<SysResourceModel>();

            string sql = $"SELECT id, name, code,pid,txt_value,type,tenant_id,create_date FROM {TableName} where 1=1 {(tenantId != 1 ? "and tenant_id=@tenantId" : "")} and pid=@pid and is_delete = 0 and is_enable = 1";
            var parameters = new Dictionary<string, object>();
            if (tenantId != -1)
                parameters.Add("@tenantId", tenantId);
            parameters.Add("@pid", pid);
            var dInfo = GetData(sql, parameters);
            foreach (DataRow item in dInfo.Rows)
            {
                SysResourceModel? model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }

        internal List<SysResourceModel> GetAllType(int type)
        {
            List<SysResourceModel> list = new List<SysResourceModel>();
            DataTable d_info = GetTableAllByType(type);
            foreach (var item in d_info.AsEnumerable())
            {
                SysResourceModel? model = GetModelFromDataRow(item);
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
            string sqlCode = string.Join(',', codes);
            string sql = $"update {TableName} set is_delete=1 where code in ('{sqlCode}')";
            return ExecuteNonQuery(sql);
        }
    }
}
