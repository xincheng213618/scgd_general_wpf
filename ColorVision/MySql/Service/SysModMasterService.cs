using ColorVision.MySql.DAO;
using System.Collections.Generic;

namespace ColorVision.MySql.Service
{
    public class SysModMasterService
    {
        private SysModMasterDao masterDao;

        public SysModMasterService()
        {
            this.masterDao = new SysModMasterDao();
        }

        internal List<SysModMasterModel> GetAll(int tenantId)
        {
            return this.masterDao.GetAll(tenantId);
        }
    }
}
