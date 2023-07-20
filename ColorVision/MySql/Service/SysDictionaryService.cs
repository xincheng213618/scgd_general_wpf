using ColorVision.MySql.DAO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.Service
{
    public class SysDictionaryService
    {
        public static string service_type_code = "service_type";

        private SysDictionaryDao sysDictionaryDao;

        public SysDictionaryService()
        {
            this.sysDictionaryDao = new SysDictionaryDao();
        }

        public List<SysDictionaryModel> GetAllServiceType()
        {
            return sysDictionaryDao.GetAllByPcode(service_type_code);
        }
    }
}
