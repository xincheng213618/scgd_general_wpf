using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using ColorVision.MySql.DAO;
using ColorVision.Template;
using ColorVision.Util;
using Org.BouncyCastle.Crypto.Parameters;

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
        private SysResourceDao resourceDao;
        public ModService()
        {
            this.masterFlowDao = new ModMasterDao(ModMasterType.Flow);
            this.masterAoiDao = new ModMasterDao(ModMasterType.Aoi);
            this.detailDao = new ModDetailDao();
            this.sysDao = new SysDictionaryModDetailDao();
            this.sysDicDao = new SysDictionaryModDao();
            this.resourceDao = new SysResourceDao();
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
            List<ModDetailModel> de = detailDao.GetAllByPid(id);
            int ret = masterFlowDao.DeleteById(id);
            detailDao.DeleteAllByPid(id);
            if(de != null && de.Count>0)
            {
                string[] codes = new string[de.Count];
                int idx = 0;
                foreach (ModDetailModel model in de)
                {
                    string code = Cryptography.GetMd5Hash(model.ValueA + model.Id);
                    codes[idx++] = code;
                }
                resourceDao.DeleteInCodes(codes);
            }

            return ret;
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
            ModDetailModel fn = flowParam.GetParameter(FlowParam.FileNameKey);
            if (fn == null)
            {
                return;
            }
            string code = Cryptography.GetMd5Hash(fn.ValueA ?? string.Empty + fn.Id ?? string.Empty);
            SysResourceModel res = resourceDao.GetByCode(code);
            if(res != null)
            {
                res.Code = code;
                res.Name = flowParam.Name;
                res.Value = flowParam.DataBase64;
                resourceDao.Save(res);
            }
            else
            {
                res = new SysResourceModel();
                res.Code = code;
                res.Name = flowParam.Name;
                res.Type = 101;
                res.Value = flowParam.DataBase64;
                resourceDao.Save(res);
            }
        }

        internal void Save(AoiParam aoiParam)
        {
            List<ModDetailModel> list = new List<ModDetailModel>();
            aoiParam.GetDetail(list);
            detailDao.UpdateByPid(aoiParam.ID, list);
        }
    }
}
