using ColorVision.Engine.MySql.ORM;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Engine.Services.Dao
{
    public class SysResourceModel : PKModel
    {

        public SysResourceModel(SysDeviceModel sysDeviceModel)
        {
            Code = sysDeviceModel.Code; 
            Id = sysDeviceModel.Id;
            Name = sysDeviceModel.Name;
            Pid = sysDeviceModel.Pid;
            Type = sysDeviceModel.Type; 
            TenantId = sysDeviceModel.TenantId; 
            Value = sysDeviceModel.Value;
            CreateDate = sysDeviceModel.CreateDate;
        }

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

        [Column("name")]
        public string? Name { get; set; }
        [Column("code")]
        public string? Code { get; set; }
        [Column("type")]
        public int Type { get; set; }
        [Column("pid")]
        public int? Pid { get; set; }
        [Column("txt_value")]
        public string? Value { get; set; }
        [Column("create_date")]
        public DateTime CreateDate { get; set; }
        [Column("tenant_id")]
        public int TenantId { get; set; }


        public string? Remark { get; set; }
    }

    public class SysResourceDao : BaseTableDao<SysResourceModel>
    {
        public static SysResourceDao Instance { get; set; } =  new SysResourceDao();
        public SysResourceDao() : base("t_scgd_sys_resource", "id") { }

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
            List<SysResourceModel> list = new();

            string sql = "SELECT rg.group_id, r.id, r.name, r.code, r.type , r.pid, r.txt_value, r.create_date, r.tenant_id, r.remark FROM t_scgd_sys_resource_group rg JOIN t_scgd_sys_resource r ON rg.resource_id = r.id WHERE  rg.group_id =@groupId";
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

        public List<SysResourceModel> GetAllCameraId() => GetAllByParam(new Dictionary<string, object>() { { "type", 101 } });
        public List<SysResourceModel> GetAllEmptyCameraId() => GetAllByParam(new Dictionary<string, object>() { { "type", 101 }}).Where(a =>string.IsNullOrWhiteSpace(a.Value)).ToList();
      
        public SysResourceModel? GetByCode(string code) => GetByParam(new Dictionary<string, object>() { { "code", code } });

        public void CreatResourceGroup()
        {
            string sql = "CREATE TABLE IF NOT EXISTS `t_scgd_sys_resource_group` ( `id` INT(11) NOT NULL AUTO_INCREMENT, `resource_id` INT(11) NOT NULL, `group_id` INT(11) NOT NULL, PRIMARY KEY (`id`), UNIQUE KEY `resource_group_unique` (`resource_id`, `group_id`), CONSTRAINT `fk_resource_id` FOREIGN KEY (`resource_id`) REFERENCES `t_scgd_sys_resource` (`id`) ON DELETE CASCADE ON UPDATE CASCADE, CONSTRAINT `fk_group_id` FOREIGN KEY (`group_id`) REFERENCES `t_scgd_sys_resource` (`id`) ON DELETE CASCADE ON UPDATE CASCADE ) ENGINE = INNODB CHARSET = utf8mb4;";
            ExecuteNonQuery(sql);
        }


        public List<SysResourceModel> GetResourceItems(int pid, int tenantId = 0)
        {
            List<SysResourceModel> list = new();

            string sql = $"SELECT * FROM {TableName} where 1=1 {(tenantId != 1 ? "and tenant_id=@tenantId" : "")} and pid=@pid and is_delete = 0 and is_enable = 1";
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

        public List<SysResourceModel> GetAllType(int type) => GetAllByParam(new Dictionary<string, object>() { { "type", type },{ "is_delete",0 } });
    }




    public class VSysResourceDao : BaseViewDao<SysResourceModel>
    {
        public static VSysResourceDao Instance { get; set; } = new VSysResourceDao();

        public VSysResourceDao() : base("v_scgd_sys_resource", "t_scgd_sys_resource", "id", true)
        {

        }

        internal List<SysResourceModel> GetServices(int tenantId)
        {
            List<SysResourceModel> list = new();
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
