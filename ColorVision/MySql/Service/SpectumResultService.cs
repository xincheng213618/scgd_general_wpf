using ColorVision.MySql.DAO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.Service
{
    public class SpectumResultService
    {
        private SpectumResultDao resultDao;

        public SpectumResultService()
        {
            resultDao = new SpectumResultDao();
        }

        internal int DeleteById(int id)
        {
            return resultDao.DeleteById(id);
        }

        internal List<SpectumResultModel> SelectByBid(string bid)
        {
            return resultDao.selectByBId(bid);
        }

        internal List<SpectumResultModel> SelectByPid(int pid)
        {
            return resultDao.GetAllByPid(pid);
        }
    }
}
