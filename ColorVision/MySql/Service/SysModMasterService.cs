using ColorVision.MySql.DAO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
