using ColorVision.MySql.DAO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.Service
{
    public class MeasureService
    {
        private MeasureMasterDao measureMaster;

        public MeasureService()
        {
            this.measureMaster = new MeasureMasterDao();
        }

        internal List<MeasureMasterModel> GetAll(int tenantId)
        {
            throw new NotImplementedException();
        }
    }
}
