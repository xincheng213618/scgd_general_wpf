using ColorVision.Database;
using SqlSugar;

namespace ColorVision.Engine.Services
{
    public class ServiceFileBase : ServiceBase
    {
        public ServiceFileBase(SysResourceModel sysResourceModel):base(sysResourceModel)
        {
            FilePath = sysResourceModel.Value;
        }
        public string? FilePath { get; set; }
    }

    public class ServiceBase : ServiceObjectBase
    {
        public SysResourceModel SysResourceModel { get; set; }
        public int Id { get => SysResourceModel.Id; set { } }

        public ServiceBase(SysResourceModel sysResourceModel)
        {
            SysResourceModel = sysResourceModel;
            Name = sysResourceModel.Name ?? string.Empty;
        }

        public override void Save()
        {
            SysResourceModel.Name = Name;
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, });
            Db.Updateable(SysResourceModel).ExecuteCommand();
        }

        public override void Delete()
        {
            base.Delete();
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, });
            int ret = Db.Deleteable<SysResourceModel>().Where(it => it.Id == SysResourceModel.Id).ExecuteCommand();
        }

    }


}
