using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using ColorVision.MySql.DAO;
using ColorVision.Template;

namespace ColorVision.MySql.service
{
    public class FlowService
    {
        private FlowMasterDao masterDao;
        private FlowDetailDao detailDao;
        private SysDictionaryModDetailDao sysDao;
        private SysDictionaryModDao sysDicDao;

        public FlowService()
        {
            masterDao = new FlowMasterDao();
            detailDao = new FlowDetailDao();
            sysDao = new SysDictionaryModDetailDao();
            sysDicDao = new SysDictionaryModDao();
        }

        internal List<FlowDetailModel> GetDetailByPid(int pkId)
        {
            return detailDao.GetAllByPid(pkId);
        }

        internal List<FlowMasterModel> GetFlowAll(int tenantId)
        {
           return masterDao.GetAll(tenantId);
        }

        internal FlowMasterModel? GetMasterById(int pkId)
        {
            return masterDao.GetByID(pkId);
        }

        internal int MasterDeleteById(int id)
        {
           return masterDao.DeleteById(id);
        }

        internal int Save(FlowMasterModel flowMaster)
        {
            int ret = -1;
            SysDictionaryModModel mod = sysDicDao.GetByCode(masterDao.GetPCode(), flowMaster.TenantId);
            if(mod != null)
            {
                flowMaster.Pid = mod.Id;
                ret = masterDao.Save(flowMaster);
                List<FlowDetailModel> list = new List<FlowDetailModel>();
                List<SysDictionaryModDetaiModel> sysDic = sysDao.GetAllByPid(flowMaster.Pid);
                foreach (var item in sysDic)
                {
                    list.Add(new FlowDetailModel(item.Id, flowMaster.Id));
                }
                detailDao.SaveByPid(flowMaster.Id, list);
            }
            return ret;
        }

        internal void Save(FlowParam flowParam)
        {
            List<FlowDetailModel> list = new List<FlowDetailModel>();
            flowParam.GetDetail(list);
            detailDao.SaveByPid(flowParam.ID, list);
        }
    }
}
