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

        internal int DetailDeleteById(int id)
        {
            return measureDetail.DeleteById(id);
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

        internal int Save(MeasureMasterModel model)
        {
            return measureMaster.Save(model);
        }

        internal int Save(MeasureDetailModel detailModel)
        {
            return measureDetail.Save(detailModel);
        }
    }
}
