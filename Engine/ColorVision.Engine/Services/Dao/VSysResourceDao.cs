using ColorVision.Database;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Data;

namespace ColorVision.Engine.Services.Dao
{
    [SugarTable("t_scgd_sys_resource")]
    public class SysResourceModel : PKModel
    {

        public SysResourceModel() 
        {
        }

        [SugarColumn(ColumnName ="name")]
        public string? Name { get; set; }
        [SugarColumn(ColumnName ="code")]
        public string? Code { get; set; }
        [SugarColumn(ColumnName ="type")]
        public int Type { get; set; }

        [SugarColumn(ColumnName ="pid",IsNullable =true)]
        public int? Pid { get; set; }

        [SugarColumn(ColumnName ="txt_value", ColumnDataType = "longtext", IsNullable = true)]
        public string? Value { get; set; }

        [SugarColumn(ColumnName = "create_date", IsNullable = true)]
        public DateTime CreateDate { get; set; } = DateTime.Now;

        [SugarColumn(ColumnName = "is_enable")]
        public bool IsEnable { get; set; } = true;

        [SugarColumn(ColumnName = "is_delete")]
        public bool IsDelete { get; set; }

        [SugarColumn(ColumnName ="tenant_id")]
        public int TenantId { get; set; }

        [SugarColumn(ColumnName = "remark",ColumnDataType ="text",IsNullable = true)]
        public string? Remark { get; set; }
    }

    public class SysResourceDao : BaseTableDao<SysResourceModel>
    {
        public static SysResourceDao Instance { get; set; } =  new SysResourceDao();
        public SysResourceDao() : base() 
        {
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
      
        public SysResourceModel? GetByCode(string code) => this.GetByParam(new Dictionary<string, object>() { { "code", code } });

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

        public List<SysResourceModel> GetAllType(int type) => this.GetAllByParam(new Dictionary<string, object>() { { "type", type },{ "is_delete",0 } });
    }

}
