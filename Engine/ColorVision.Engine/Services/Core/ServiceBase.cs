using ColorVision.Database;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using SqlSugar;
using static iText.StyledXmlParser.Jsoup.Select.Evaluator;

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
            MySqlControl.GetInstance().DB.Updateable(SysResourceModel).ExecuteCommand();
        }

        public override void Delete()
        {
            base.Delete();
            int ret = Db.Deleteable<SysResourceModel>().Where(it => it.Id == SysResourceModel.Id).ExecuteCommand();
        }

    }


}
