#pragma warning disable CS8603
using ColorVision.Services.Dao;
using ColorVision.Services.DAO;
using ColorVision.Services.Devices.Algorithm.Dao;
using ColorVision.Services.Devices.SMU.Dao;
using ColorVision.Services.Devices.Spectrum.Dao;
using System.Collections.Generic;

namespace ColorVision.MySql.Service
{
    public class ResultService
    {
        private SpectumResultDao spectumDao;
        private SMUResultDao smuDao;
        private BatchResultMasterDao batchDao;
        private AlgResultMasterDao algResultMasterDao;
        private POIPointResultDao poiPointResultDao;

        private AlgResultMTFDao MTFResultDao;
        private AlgResultGhostDao GhostResultDao;
        private AlgResultDistortionDao DisResultDao;
        public ResultService()
        {
            spectumDao = new SpectumResultDao();
            smuDao = new SMUResultDao();
            batchDao = new BatchResultMasterDao();
            poiPointResultDao = new POIPointResultDao();
            algResultMasterDao = new AlgResultMasterDao();

            MTFResultDao = new AlgResultMTFDao();
            GhostResultDao = new AlgResultGhostDao();
            DisResultDao = new AlgResultDistortionDao();
        }

        internal List<SpectumResultModel> SpectumSelectBySN(string sn)
        {
            BatchResultMasterModel batch = batchDao.GetByCode(sn);
            if(batch == null)  return spectumDao.selectBySN(sn);
            else return spectumDao.GetAllByPid(batch.Id);
        }

        internal List<SMUResultModel> SMUSelectBySN(string sn)
        {
            BatchResultMasterModel batch = batchDao.GetByCode(sn);
            if (batch == null) return smuDao.selectBySN(sn);
            else return smuDao.GetAllByPid(batch.Id);
        }

        internal List<SpectumResultModel> SpectumSelectByPid(int pid)
        {
            return spectumDao.GetAllByPid(pid);
        }

        public int BatchSave(BatchResultMasterModel model)
        {
            return batchDao.Save(model);
        }

        internal int BatchUpdateEnd(string bid, int totalTime, string result)
        {
            return batchDao.UpdateEnd(bid, totalTime, result);
        }

        public List<AlgResultMasterModel>? GetAlgResultBySN(string serialNumber)
        {
            return algResultMasterDao.GetAllByBatchCode(serialNumber);
        }

        public AlgResultMasterModel GetAlgResultById(int id)
        {
            return algResultMasterDao.GetById(id);
        }

        public List<POIPointResultModel> GetPOIByPid(int pid)
        {
            return poiPointResultDao.GetAllByPid(pid);
        }

        public List<AlgResultMTFModel> GetMTFByPid(int pid) => MTFResultDao.GetAllByPid(pid);
        public List<AlgResultGhostModel> GetGhostByPid(int pid) => GhostResultDao.GetAllByPid(pid);

        public List<AlgResultDistortionModel> GetDistortionByPid(int pid) => DisResultDao.GetAllByPid(pid);
    }
}
