using ColorVision.MySql.DAO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.Service
{
    public class ResourceService
    {
        private ResourceDao resourceDao;

        public ResourceService()
        {
            this.resourceDao = new ResourceDao();
        }

        internal List<ResourceModel> GetAllDevices(int tenantId)
        {
            return resourceDao.GetPidIsNotNull(tenantId);
        }

        internal List<ResourceModel> GetAllServices(int tenantId)
        {
            return resourceDao.GetPidIsNull(tenantId);
        }
    }
}
