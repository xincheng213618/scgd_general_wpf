using ColorVision.Database;
using SqlSugar;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Dao
{
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
                db.Insertable(new SysResourceGoupModel{ ResourceId = resourceId,  GroupId = groupId}).ExecuteCommand();
            }
        }
        public void DeleteGroupRelate(int groupId)
        {
            using (var db = GetDb())
            {
                db.Deleteable<SysResourceGoupModel>()  .Where(x => x.GroupId == groupId) .ExecuteCommand();
            }
        }
        public void CreatResourceGroup()=> Db.CodeFirst.InitTables<SysResourceGoupModel>();


        public List<SysResourceModel> GetGroupResourceItems(int groupId)=> Db.Queryable<SysResourceGoupModel, SysResourceModel>((rg, r) => rg.ResourceId == r.Id) .Where((rg, r) => rg.GroupId == groupId).Select((rg, r) => r)  .ToList();


        public List<SysResourceModel> GetAllType(int type) => this.GetAllByParam(new Dictionary<string, object>() { { "type", type },{ "is_delete",0 } });
    }

}
