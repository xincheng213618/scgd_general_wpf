using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using ColorVision.MySql.DAO;
using ColorVision.Template;

namespace ColorVision.MySql.Service
{
    public class ModMasterType
    {
        public const string Flow = "flow"; 
        public const string Aoi = "AOI"; 
    }
    public class ModService
    {
        private ModMasterDao masterFlowDao;
        private ModMasterDao masterAoiDao;
        private ModDetailDao detailDao;
        private SysDictionaryModDetailDao sysDao;
        private SysDictionaryModDao sysDicDao;

        public ModService()
        {
            masterFlowDao = new ModMasterDao(ModMasterType.Flow);
            masterAoiDao = new ModMasterDao(ModMasterType.Aoi);
            detailDao = new ModDetailDao();
            sysDao = new SysDictionaryModDetailDao();
            sysDicDao = new SysDictionaryModDao();
        }

        internal List<ModDetailModel> GetDetailByPid(int pkId)
        {
            return detailDao.GetAllByPid(pkId);
        }

        internal List<ModMasterModel> GetFlowAll(int tenantId)
        {
           return masterFlowDao.GetAll(tenantId);
        }

        internal List<ModMasterModel> GetAoiAll(int tenantId)
        {
            return masterAoiDao.GetAll(tenantId);
        }

        internal ModMasterModel? GetMasterById(int pkId)
        {
            return masterFlowDao.GetByID(pkId);
        }

        internal int MasterDeleteById(int id)
        {
           return masterFlowDao.DeleteById(id);
        }

        internal int Save(ModMasterModel flowMaster)
        {
            int ret = -1;
            SysDictionaryModModel mod = sysDicDao.GetByCode(flowMaster.Pcode, flowMaster.TenantId);
            if(mod != null)
            {
                flowMaster.Pid = mod.Id;
                ret = masterFlowDao.Save(flowMaster);
                List<ModDetailModel> list = new List<ModDetailModel>();
                List<SysDictionaryModDetaiModel> sysDic = sysDao.GetAllByPid(flowMaster.Pid);
                foreach (var item in sysDic)
                {
                    list.Add(new ModDetailModel(item.Id, flowMaster.Id, item.DefaultValue));
                }
                detailDao.SaveByPid(flowMaster.Id, list);
            }
            return ret;
        }

        internal void Save(FlowParam flowParam)
        {
            List<ModDetailModel> list = new List<ModDetailModel>();
            flowParam.GetDetail(list);
            detailDao.UpdateByPid(flowParam.ID,list);
        }

        internal void Save(AoiParam aoiParam)
        {
            List<ModDetailModel> list = new List<ModDetailModel>();
            aoiParam.GetDetail(list);
            detailDao.UpdateByPid(aoiParam.ID, list);
        }
    }
}
