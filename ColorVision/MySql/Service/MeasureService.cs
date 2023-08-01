using ColorVision.MySql.DAO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace ColorVision.MySql.Service
{
    public class MeasureService
    {
        private MeasureMasterDao measureMaster;
        private MeasureDetailDao measureDetail;

        public MeasureService()
        {
            this.measureMaster = new MeasureMasterDao();
            this.measureDetail = new MeasureDetailDao();
        }

        internal List<MeasureMasterModel> GetAll(int tenantId)
        {
            return measureMaster.GetAll(tenantId);
        }

        internal List<MeasureDetailModel> GetDetailByPid(int pid)
        {
            return measureDetail.GetAllByPid(pid);
        }

        internal MeasureMasterModel? GetMasterById(int pkId)
        {
            return measureMaster.GetByID(pkId);
        }

        internal void Save(MeasureMasterModel model)
        {
            measureMaster.Save(model);
        }
    }
}
