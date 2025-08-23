using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Templates;
using SqlSugar;

namespace ColorVision.Engine.Services.Core
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
        public static SqlSugarClient Db => MySqlControl.GetInstance().DB;

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
            VSysResourceDao.Instance.Save(SysResourceModel);
        }

        public override void Delete()
        {
            base.Delete();  
            SysResourceDao.Instance.DeleteById(SysResourceModel.Id);
        }

    }


}
