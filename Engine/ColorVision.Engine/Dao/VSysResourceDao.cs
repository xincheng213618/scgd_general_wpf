using ColorVision.Database;
using System.Collections.Generic;

namespace ColorVision.Engine
{
    public class SysResourceDao : BaseTableDao<SysResourceModel>
    {
        public static SysResourceDao Instance { get; set; } =  new SysResourceDao();
        public SysResourceDao() : base() 
        {
        }

        public List<SysResourceModel> GetGroupResourceItems(int groupId)=> Db.Queryable<SysResourceGoupModel, SysResourceModel>((rg, r) => rg.ResourceId == r.Id) .Where((rg, r) => rg.GroupId == groupId).Select((rg, r) => r)  .ToList();


        public List<SysResourceModel> GetAllType(int type) => this.GetAllByParam(new Dictionary<string, object>() { { "type", type },{ "is_delete",0 } });
    }

}
