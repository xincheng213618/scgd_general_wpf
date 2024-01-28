using ColorVision.Services.Dao;
using System.Collections.Generic;

namespace ColorVision.MySql.Service
{
    public class SysResourceService
    {
        private SysResourceDao resourceDao = new SysResourceDao();

        internal int DeleteById(int id)
        {
            return resourceDao.DeleteById(id);
        }

        internal int DeleteAllByPid(int pid)
        {
            return resourceDao.DeleteAllByPid(pid);
        }

        internal List<SysResourceModel> GetAllDevices(int tenantId)
        {
            return resourceDao.GetPidIsNotNull(tenantId);
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
    }
}
