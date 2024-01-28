#pragma warning disable  CS8604,CS8631
using ColorVision.MySql.Service;
using ColorVision.Services.Dao;
using System.Windows.Xps.Packaging;

namespace ColorVision.Services.Devices
{
    public class GroupService: BaseResourceObject
    {
        public static GroupService AddGroupService(DeviceService deviceService , string Name)
        {
            SysResourceModel sysResourceModel = new SysResourceModel() { Name = Name ,Type = (int)ResourceType.Group };
            sysResourceModel.Pid = deviceService.SysResourceModel.Id;
            sysResourceModel.TenantId = deviceService.SysResourceModel.TenantId;

            SysResourceDao sysResourceDao = new SysResourceDao();
            sysResourceDao.Save(sysResourceModel);

            int pkId = sysResourceModel.GetPK();
            if (pkId > 0 && sysResourceDao.GetById(pkId) is SysResourceModel model)
            {
                GroupService groupService = new GroupService(model);
                deviceService.AddChild(groupService);
                return groupService;
            }
            return null;
        }

        public GroupService(SysResourceModel sysResourceModel)
        {
            SysResourceModel = sysResourceModel;
            Name = sysResourceModel.Name ?? sysResourceModel.Id.ToString();
        }

        SysResourceDao SysResourceDao = new SysResourceDao();

        public override void Save()
        {
            SysResourceModel.Name = Name;
            SysResourceDao.Save(SysResourceModel);
            base.Save();
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
