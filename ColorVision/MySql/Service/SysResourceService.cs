using ColorVision.MySql.DAO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.Service
{
    public class SysResourceService
    {
        private SysResourceDao resourceDao;

        public SysResourceService()
        {
            this.resourceDao = new SysResourceDao();
        }

        internal List<SysResourceModel> GetAllDevices(int tenantId)
        {
            return resourceDao.GetPidIsNotNull(tenantId);
        }

        internal List<SysResourceModel> GetAllServices(int tenantId)
        {
            return resourceDao.GetPidIsNull(tenantId);
        }
    }
}
