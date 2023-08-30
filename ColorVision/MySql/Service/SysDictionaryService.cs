using ColorVision.MySql.DAO;
using System.Collections.Generic;

namespace ColorVision.MySql.Service
{
    public class SysDictionaryService
    {
        public static string ServiceTypeCode { get; set; } = "service_type";

        private SysDictionaryDao sysDictionaryDao;

        public SysDictionaryService()
        {
            this.sysDictionaryDao = new SysDictionaryDao();
        }

        public List<SysDictionaryModel> GetAllServiceType()
        {
            return sysDictionaryDao.GetAllByPcode(ServiceTypeCode);
        }
    }
}
