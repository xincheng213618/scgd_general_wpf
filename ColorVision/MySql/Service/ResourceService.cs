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
            List<ResourceModel> result = new List<ResourceModel>();
            List<ResourceModel> tps = resourceDao.GetAllByType(type, tenantId);
            foreach (var dbModel in tps)
            {
                result.AddRange(resourceDao.GetAllByPid(dbModel.Id));
            }
            return result;
        }
    }
}
