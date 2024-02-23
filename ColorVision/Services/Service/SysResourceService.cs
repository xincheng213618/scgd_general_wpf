using ColorVision.Services.Dao;
using System.Collections.Generic;

namespace ColorVision.MySql.Service
{
    public class SysResourceService
    {
        private VSysResourceDao resourceDao = new VSysResourceDao();
        private VSysDeviceDao deviceDao = new VSysDeviceDao();

        internal int DeleteById(int id)
        {
            return resourceDao.DeleteById(id);
        }

        internal int DeleteAllByPid(int pid)
        {
            return resourceDao.DeleteAllByPid(pid);
        }

        internal List<SysDeviceModel> GetAllDevices(int tenantId)
        {
            return deviceDao.GetAll();
        }
        internal SysDeviceModel? GetDeviceById(int pkId)
        {
            return deviceDao.GetById(pkId);
        }

        internal List<SysResourceModel> GetAllServices(int tenantId)
        {
            return resourceDao.GetServices(tenantId);
        }

        internal SysResourceModel? GetByCode(string code)
        {
            return resourceDao.GetByCode(code);
        }

        internal SysResourceModel? GetMasterById(int pkId)
        {
            return resourceDao.GetById(pkId);
        }

        internal void Save(SysResourceModel sysResource)
        {
            resourceDao.Save(sysResource);
        }
        internal void Save(SysDeviceModel sysDev)
        {
            SysResourceModel sysResource = new SysResourceModel { CreateDate = sysDev.CreateDate, Code = sysDev.Code, Id= sysDev.Id,
             Name = sysDev.Name, Pid = sysDev.Pid, Type = sysDev.Type, TenantId = sysDev.TenantId, TypeCode = sysDev.TypeCode, Value = sysDev.Value };
            resourceDao.Save(sysResource);
        }
    }
}
