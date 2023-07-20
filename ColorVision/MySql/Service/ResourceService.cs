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

        public List<ResourceModel> GetByType(int type,int tenantId)
        {
            return resourceDao.GetAllByType(type, tenantId);
        }
    }
}
