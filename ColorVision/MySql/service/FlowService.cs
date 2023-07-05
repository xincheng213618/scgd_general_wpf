using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ColorVision.MySql.DAO;

namespace ColorVision.MySql.service
{
    public class FlowService
    {
        private FlowMasterDao masterDao;
        private FlowDetailDao detailDao;

        public FlowService()
        {
            masterDao = new FlowMasterDao();
            detailDao = new FlowDetailDao();
        }
        internal List<FlowMasterModel> GetFlowAll(int tenantId)
        {
           return masterDao.GetAll(tenantId);
        }

        internal void Save(FlowMasterModel flowMaster)
        {
            masterDao.Save(flowMaster);
        }
    }
}
