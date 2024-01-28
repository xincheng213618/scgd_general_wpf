using ColorVision.Services.Dao;
using System.Collections.Generic;

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
            return measureMaster.GetById(pkId);
        }

        internal int MasterDeleteById(int id)
        {
            int ret = measureMaster.DeleteById(id);
            if (ret == 1)
            {
               int iret =  measureDetail.DeleteAllByPid(id);
            }
            return ret;
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
