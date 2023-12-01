using System.Collections.Generic;
using ColorVision.MySql.DAO;
using ColorVision.Templates;

namespace ColorVision.MySql.Service
{
    public partial class ModMasterType
    {
        public const string Flow = "flow"; 
        public const string Aoi = "AOI"; 
        public const string SMU = "SMU"; 
        public const string PG = "pg";
        public const string MTF = "MTF";
        public const string SFR = "SFR";
        public const string FOV = "FOV";
        public const string POI = "POI";
        public const string Ghost = "ghost";
        public const string Distortion = "distortion";
        public const string LedCheck = "ledcheck";
        public const string FocusPoints = "focusPoints";
        public const string Calibration = "Calibration";

    }
    public class ModService
    {
        private ModMasterDao masterFlowDao;
        private ModMasterDao masterAoiDao;
        private ModMasterDao masterModDao;
        private ModMasterDao masterSMUDao;
        private ModMasterDao masterPGDao;
        private ModMasterDao masterMTFDao;

        private ModDetailDao detailDao;

        private SysDictionaryModDetailDao sysDao;
        private SysDictionaryModDao sysDicDao;
        private SysResourceDao resourceDao;
        public ModService()
        {
            this.masterFlowDao = new ModMasterDao(ModMasterType.Flow);
            this.masterAoiDao = new ModMasterDao(ModMasterType.Aoi);
            this.masterSMUDao = new ModMasterDao(ModMasterType.SMU);
            this.masterPGDao = new ModMasterDao(ModMasterType.PG);
            this.masterMTFDao = new ModMasterDao(ModMasterType.MTF);
            this.masterModDao = new ModMasterDao();
            this.detailDao = new ModDetailDao();
            this.sysDao = new SysDictionaryModDetailDao();
            this.sysDicDao = new SysDictionaryModDao();
            this.resourceDao = new SysResourceDao();
        }

        internal List<ModDetailModel> GetDetailByPid(int pkId)
        {
            return detailDao.GetAllByPid(pkId);
        }

        internal List<ModMasterModel> GetMTFAll(int tenantId)
        {
            return masterMTFDao.GetAll(tenantId);
        }

        internal List<ModMasterModel> GetPGAll(int tenantId)
        {
            return masterPGDao.GetAll(tenantId);
        }
        internal List<ModMasterModel> GetSMUAll(int tenantId)
        {
            return masterSMUDao.GetAll(tenantId);
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
                    string code = model.GetValueMD5();
                    codes[idx++] = code;
                }
                resourceDao.DeleteInCodes(codes);
            }

            return ret;
        }

        internal int Save(ModMasterModel modMaster)
        {
            int ret = -1;
            SysDictionaryModModel mod = sysDicDao.GetByCode(modMaster.Pcode, modMaster.TenantId);
            if(mod != null)
            {
                modMaster.Pid = mod.Id;
                ret = masterFlowDao.Save(modMaster);
                List<ModDetailModel> list = new List<ModDetailModel>();
                List<SysDictionaryModDetaiModel> sysDic = sysDao.GetAllByPid(modMaster.Pid);
                foreach (var item in sysDic)
                {
                    list.Add(new ModDetailModel(item.Id, modMaster.Id, item.DefaultValue));
                }
                detailDao.SaveByPid(modMaster.Id, list);
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
            string code = fn.GetValueMD5();
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

        internal List<ModMasterModel> GetMasterByPid(int pid)
        {
           return masterModDao.GetAllByPid(pid);
        }

        internal void Save(ParamBase value)
        {
            List<ModDetailModel> list = new List<ModDetailModel>();
            value.GetDetail(list);
            detailDao.UpdateByPid(value.ID, list);
        }
    }
}
