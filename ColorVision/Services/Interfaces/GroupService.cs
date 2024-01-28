#pragma warning disable  CS8604,CS8631
using ColorVision.MySql.Service;
using ColorVision.Services.Dao;
using System.Windows.Xps.Packaging;

namespace ColorVision.Services.Devices
{
    public class GroupService: BaseResourceObject
    {
        public static bool AddGroupService(DeviceService deviceService , string Name)
        {
            SysResourceModel sysResourceModel = new SysResourceModel() { Name = Name ,Type = (int)ResourceType.Group };
            sysResourceModel.Pid = deviceService.SysResourceModel.Id;
            sysResourceModel.TenantId = deviceService.SysResourceModel.TenantId;

            VSysResourceDao sysResourceDao = new VSysResourceDao();
            sysResourceDao.Save(sysResourceModel);

            int pkId = sysResourceModel.GetPK();
            if (pkId > 0 && sysResourceDao.GetById(pkId) is SysResourceModel model)
            {
                deviceService.AddChild(new GroupService(model));
                return true;
            }
            return false;
        }

        public GroupService(SysResourceModel sysResourceModel)
        {
            SysResourceModel = sysResourceModel;
            Name = sysResourceModel.Name ?? sysResourceModel.Id.ToString();
        }


        public SysResourceModel SysResourceModel { get; set; }

        public override void AddChild(BaseResourceObject baseObject)
        {
            base.AddChild(baseObject);
        }

        public override void RemoveChild(BaseResourceObject baseObject)
        {
            base.RemoveChild(baseObject);
        }
    }
}
