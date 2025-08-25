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
    [SugarTable("t_scgd_sys_resource_group")]
    public class ResourceGoup:PKModel
    {

        [SugarColumn(ColumnName = "resource_id")]
        public int ResourceId { get; set; }
        [SugarColumn(ColumnName = "group_id")]
        public int GroupId { get; set; }

        [SugarColumn(IsIgnore = true)]
        public SysResourceModel Group { get; set; }
        [SugarColumn(IsIgnore = true)]
        public SysResourceModel Resourced { get; set; }
    }

    public class SysResourceDao : BaseTableDao<SysResourceModel>
    {
        public static SysResourceDao Instance { get; set; } =  new SysResourceDao();
        public SysResourceDao() : base() 
        {
        }
        private SqlSugarClient GetDb()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true
            });
        }
        public void ADDGroup(int groupId ,int resourceId)
        {
            using (var db = GetDb())
            {
                db.Insertable(new ResourceGoup{ ResourceId = resourceId,  GroupId = groupId}).ExecuteCommand();
            }
        }
        public void DeleteGroupRelate(int groupId)
        {
            using (var db = GetDb())
            {
                db.Deleteable<ResourceGoup>()  .Where(x => x.GroupId == groupId) .ExecuteCommand();
            }
        }
        public void CreatResourceGroup()=> Db.CodeFirst.InitTables<ResourceGoup>();


        public List<SysResourceModel> GetGroupResourceItems(int groupId)=> Db.Queryable<ResourceGoup, SysResourceModel>((rg, r) => rg.ResourceId == r.Id) .Where((rg, r) => rg.GroupId == groupId).Select((rg, r) => r)  .ToList();


        public List<SysResourceModel> GetAllType(int type) => this.GetAllByParam(new Dictionary<string, object>() { { "type", type },{ "is_delete",0 } });
    }

}
