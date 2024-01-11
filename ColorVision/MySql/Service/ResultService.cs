#pragma warning disable CS8603
using ColorVision.MySql.DAO;
using ColorVision.Services.Device.Algorithm.Dao;
using ColorVision.Services.Device.SMU.Dao;
using ColorVision.Services.Device.Spectrum.Dao;
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
        private MeasureImgResultDao measureImgResultDao;

        private AlgResultFOVDao FOVResultDao;
        private AlgResultSFRDao SFRResultDao;
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
            measureImgResultDao = new MeasureImgResultDao();

            FOVResultDao = new AlgResultFOVDao();
            SFRResultDao = new AlgResultSFRDao();
            MTFResultDao = new AlgResultMTFDao();
            GhostResultDao = new AlgResultGhostDao();
            DisResultDao = new AlgResultDistortionDao();
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
        public MeasureImgResultModel GetCameraImgResultById(int id)
        {
            return measureImgResultDao.GetById(id);
        }

        public List<MeasureImgResultModel>? GetCameraImgResultBySN(string serialNumber)
        {
            return measureImgResultDao.GetAllByBatchCode(serialNumber);
        }

        public List<AlgResultFOVModel> GetFOVByPid(int pid) => FOVResultDao.GetAllByPid(pid);

        public List<AlgResultSFRModel> GetSFRByPid(int pid) => SFRResultDao.GetAllByPid(pid);

        public List<AlgResultMTFModel> GetMTFByPid(int pid) => MTFResultDao.GetAllByPid(pid);
        public List<AlgResultGhostModel> GetGhostByPid(int pid) => GhostResultDao.GetAllByPid(pid);

        public List<AlgResultDistortionModel> GetDistortionByPid(int pid) => DisResultDao.GetAllByPid(pid);
    }
}
