using ColorVision.MySql.DAO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.Service
{
    public class ResultService
    {
        private SpectumResultDao spectumDao;
        private BatchResultMasterDao batchDao;

        public ResultService()
        {
            spectumDao = new SpectumResultDao();
            batchDao = new BatchResultMasterDao();
        }

        internal int SpectumDeleteById(int id)
        {
            return spectumDao.DeleteById(id);
        }

        internal List<SpectumResultModel> SpectumSelectBySN(string sn)
        {
            BatchResultMasterModel batch = batchDao.GetByCode(sn);
            if(batch == null)  return spectumDao.selectBySN(sn);
            else return spectumDao.GetAllByPid(batch.Id);
        }

        internal List<SpectumResultModel> SpectumSelectByPid(int pid)
        {
            return spectumDao.GetAllByPid(pid);
        }

        public int BatchSave(BatchResultMasterModel model)
        {
            return batchDao.Save(model);
        }
    }
}
