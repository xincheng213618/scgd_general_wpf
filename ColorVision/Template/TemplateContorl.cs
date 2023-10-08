using ColorVision.Extension;
using ColorVision.MySql;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.SettingUp;
using ColorVision.Util;
using cvColorVision.Util;
using NPOI.SS.Formula.Functions;
using NPOI.XWPF.UserModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace ColorVision.Template
{
    /// <summary>
    /// 模板管理
    /// </summary>

    public class TemplateControl
    {
        private static TemplateControl _instance;
        private static readonly object _locker = new();
        public static TemplateControl GetInstance() { lock (_locker) { return _instance ??= new TemplateControl(); } }

        private static string FileNameAoiParams = "cfg\\AOIParamSetup.cfg";
        private static string FileNameCalibrationParams = "cfg\\CalibrationSetup.cfg";
        private static string FileNamePGParams = "cfg\\PGParamSetup.cfg";

        private static string FileNameLedJudgeParams = "cfg\\LedJudgeSetup.cfg";
        private static string FileNameSxParms = "cfg\\SxParamSetup.cfg";
        private static string FileNamePoiParms = "cfg\\PoiParmSetup.cfg";
        private static string FileNameFlowParms = "cfg\\FlowParmSetup.cfg";

        private PoiService poiService = new PoiService();
        private ModService modService = new ModService();
        private SysModMasterService sysModService = new SysModMasterService();
        private SysResourceService resourceService = new SysResourceService();
        private SysDictionaryService dictionaryService = new SysDictionaryService();
        private MeasureService measureService = new MeasureService();

        public TemplateControl()
        {
            if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory+ "cfg"))
                Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "cfg");

            AoiParams = new ObservableCollection<KeyValuePair<string, AoiParam>>();
            CalibrationParams = new ObservableCollection<KeyValuePair<string, CalibrationParam>>();
            PGParams = new ObservableCollection<KeyValuePair<string, PGParam>>();
            LedReusltParams = new ObservableCollection<KeyValuePair<string, LedReusltParam>>();
            SMUParams = new ObservableCollection<KeyValuePair<string, SMUParam>>();
            FlowParams = new ObservableCollection<KeyValuePair<string, FlowParam>>();
            PoiParams = new ObservableCollection<KeyValuePair<string, PoiParam>>();
            MeasureParams = new ObservableCollection<KeyValuePair<string, MeasureParam>>();

            MTFParams = new ObservableCollection<KeyValuePair<string, MTFParam>>();
            SFRParams = new ObservableCollection<KeyValuePair<string, SFRParam>>();
            

            GlobalSetting.GetInstance().SoftwareConfig.UseMySqlChanged += (s) =>
            {
                Init();
            };

            Init();

            Application.Current.MainWindow.Closed += (s, e) =>
            {
                if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
                    return;
                CSVSave();
            };
        }
        private void Init()
        {
            CalibrationParams = IDefault(FileNameCalibrationParams, new CalibrationParam());
            LedReusltParams = IDefault(FileNameLedJudgeParams, new LedReusltParam());
            LoadPoiParam();
            LoadFlowParam();

            LoadModParam(AoiParams, ModMasterType.Aoi);
            LoadModParam(SMUParams, ModMasterType.SMU);
            LoadModParam(PGParams, ModMasterType.PG);
            LoadModParam(SFRParams, ModMasterType.SFR);
            LoadModParam(MTFParams, ModMasterType.MTF);
        }

        /// 这里是初始化模板的封装，因为模板的代码高度统一，所以使用泛型T来设置具体的模板参数。
        /// 最后在给模板的每一个元素加上一个切换的效果，即当某一个模板启用时，关闭其他已经启用的模板；
        /// 同一类型，只能存在一个启用的模板
        private static ObservableCollection<KeyValuePair<string, T>> IDefault<T>(string FileName ,T Default) where T : ParamBase
        {
            ObservableCollection<KeyValuePair<string, T>> Params = new ObservableCollection<KeyValuePair<string, T>>();

            Params = CfgFile.Load<ObservableCollection<KeyValuePair<string, T>>>(FileName) ?? new ObservableCollection<KeyValuePair<string, T>>();
            if (Params.Count == 0)
            {
                Params.Add(new KeyValuePair<string, T>("default", Default));
            }

            foreach (var item in Params)
            {
                item.Value.IsEnabledChanged += (s, e) =>
                {
                    foreach (var item2 in Params)
                    {
                        if (item2.Key != item.Key)
                            item2.Value.IsEnable = false;
                    }
                };
            }
            Params.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    Params[e.NewStartingIndex].Value.IsEnabledChanged += (s, e1) =>
                    {
                        foreach (var item2 in Params)
                        {
                            if (item2.Key != Params[e.NewStartingIndex].Key)
                                item2.Value.IsEnable = false;
                        }
                    };

                }
            };
            return Params;
        }



        public void CSVSave()
        {
            SaveDefault(FileNameAoiParams, AoiParams);
            SaveDefault(FileNameCalibrationParams, CalibrationParams);
            SaveDefault(FileNamePGParams, PGParams);
            SaveDefault(FileNameLedJudgeParams, LedReusltParams);
            SaveDefault(FileNameSxParms, SMUParams);
            SaveDefault(FileNamePoiParms, PoiParams);
            SaveDefault(FileNameFlowParms, FlowParams);
        }


        public void Save(WindowTemplateType windowTemplateType)
        {
            switch (windowTemplateType)
            {
                case WindowTemplateType.Calibration:
                    SaveDefault(FileNameCalibrationParams, CalibrationParams);
                    break;
                case WindowTemplateType.LedReuslt:
                    SaveDefault(FileNameLedJudgeParams, LedReusltParams);
                    break;
                case WindowTemplateType.AoiParam:
                    Save(AoiParams, ModMasterType.Aoi);
                    break;
                case WindowTemplateType.PGParam:
                    Save(PGParams, ModMasterType.PG);
                    break;
                case WindowTemplateType.SMUParam:
                    Save(SMUParams, ModMasterType.SMU);
                    break;
                case WindowTemplateType.MTFParam:
                    Save(MTFParams, ModMasterType.MTF);
                    break;
                case WindowTemplateType.SFRParam:
                    Save(SFRParams, ModMasterType.SFR);
                    break;
                case WindowTemplateType.PoiParam:
                    SaveDefault(FileNamePoiParms, PoiParams);
                    break;
                case WindowTemplateType.FlowParam:
                    SaveDefault(FileNameFlowParms, FlowParams);
                    break;
                default:
                    break;
            }
        }

        private void Save<T>(ObservableCollection<KeyValuePair<string, T>> t ,string code) where T: ParamBase
        {
            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
                Save2DB(t);
            else 
                SaveDefault($"cfg\\{code}.cfg", t);
        }


        public void Save2DB<T>(ObservableCollection<KeyValuePair<string, T>>  keyValuePairs) where T : ParamBase
        {
            foreach (var item in keyValuePairs)
                Save2DB(item.Value);
        }

        public void Save2DB<T>(T value) where T : ParamBase
        {
            modService.Save(value);
        }


        private static void SaveDefault<T>(string FileNameParams, ObservableCollection<KeyValuePair<string, T>> t)
        {
            CfgFile.Save(FileNameParams, t);
        }



        public ObservableCollection<KeyValuePair<string, PoiParam>> LoadPoiParam()
        {
            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
            {
                PoiParams.Clear();
                List<PoiMasterModel> poiMaster = poiService.GetMasterAll(GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
                foreach (var dbModel in poiMaster)
                {
                    KeyValuePair<string, PoiParam> item = new KeyValuePair<string, PoiParam>(dbModel.Name ?? "default", new PoiParam(dbModel));
                    PoiParams.Add(item);
                }
            }
            else
            {
                PoiParams.Clear();
                if (PoiParams.Count == 0)
                    PoiParams = IDefault(FileNamePoiParms, new PoiParam());
            }

            return PoiParams;
        }

        internal PoiParam? AddPoiParam(string text)
        {
            PoiMasterModel poiMaster = new PoiMasterModel(text, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
            poiService.Save(poiMaster);
            int pkId = poiMaster.GetPK();
            if (pkId > 0)
            {
                return LoadPoiParamById(pkId);
            }
            return null;
        }

        internal void LoadPoiDetailFromDB(PoiParam poiParam)
        {
            poiParam.PoiPoints.Clear();


            List<PoiDetailModel> poiDetail = poiService.GetDetailByPid(poiParam.ID);
            foreach (var dbModel in poiDetail)
            {
                poiParam.PoiPoints.Add(new PoiParamData(dbModel));
            }
        }


        public T? AddParamMode<T>(string code,string Name) where T: ParamBase,new ()
        {
            ModMasterModel flowMaster = new ModMasterModel(code, Name, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
            modService.Save(flowMaster);
            int pkId = flowMaster.GetPK();
            if (pkId > 0)
            {
                ModMasterModel modMasterModel = modService.GetMasterById(pkId);
                List<ModDetailModel>  modDetailModels = modService.GetDetailByPid(pkId);
                if (modMasterModel != null) return (T)Activator.CreateInstance(typeof(T), new object[] { modMasterModel, modDetailModels });
                else return null;
            }
            return null;

        }



        internal FlowParam? AddFlowParam(string text)
        {
            ModMasterModel flowMaster = new ModMasterModel(ModMasterType.Flow, text, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
            modService.Save(flowMaster);
            int pkId = flowMaster.GetPK();
            if (pkId > 0)
            {
                return LoadFlowParamById(pkId);
            }
            return null;
        }

        internal ResourceParam? AddDeviceParam(string name, string code, int type, int pid)
        {
            SysResourceModel sysResource = new SysResourceModel(name, code, type,pid, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
            resourceService.Save(sysResource);
            int pkId = sysResource.GetPK();
            if (pkId > 0)
            {
                return LoadServiceParamById(pkId);
            }
            return null;
        }

        internal ResourceParam? AddServiceParam(string name,string code,int type)
        {
            SysResourceModel sysResource = new SysResourceModel(name, code, type, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
            resourceService.Save(sysResource);
            int pkId = sysResource.GetPK();
            if (pkId > 0)
            {
                return LoadServiceParamById(pkId);
            }
            return null;
        }

        internal MeasureParam? AddMeasureParam(string name)
        {
            MeasureMasterModel model = new MeasureMasterModel(name, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
            measureService.Save(model);
            int pkId = model.GetPK();
            if (pkId > 0)
            {
                return LoadMeasureParamById(pkId);
            }
            return null;
        }
        private MeasureParam? LoadMeasureParamById(int pkId)
        {
            MeasureMasterModel model = measureService.GetMasterById(pkId);
            if (model != null) return new MeasureParam(model);
            else return null;
        }
        private ResourceParam? LoadServiceParamById(int pkId)
        {
            SysResourceModel model = resourceService.GetMasterById(pkId);
            if (model != null) return new ResourceParam(model);
            else return null;
        }

        internal PoiParam? LoadPoiParamById(int pkId)
        {
            PoiMasterModel poiMaster = poiService.GetMasterById(pkId);
            if (poiMaster != null) return new PoiParam(poiMaster);
            else return null;
        }

        private FlowParam? LoadFlowParamById(int pkId)
        {
            ModMasterModel flowMaster = modService.GetMasterById(pkId);
            List<ModDetailModel> flowDetail = modService.GetDetailByPid(pkId);
            if (flowMaster != null) return new FlowParam(flowMaster, flowDetail);
            else return null;
        }


        private void LoadModParam<T>(ObservableCollection<KeyValuePair<string, T>> ParamModes, string ModeType) where T : ParamBase,new ()
        {
            ParamModes.Clear();
            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
            {
                ModMasterDao masterFlowDao = new ModMasterDao(ModeType);

                List<ModMasterModel> smus = masterFlowDao.GetAll(GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
                foreach (var dbModel in smus)
                {
                    List<ModDetailModel> smuDetails = modService.GetDetailByPid(dbModel.Id);
                    foreach (var dbDetail in smuDetails)
                    {
                        dbDetail.ValueA = dbDetail?.ValueA?.Replace("\\r", "\r");
                    }
                    KeyValuePair<string, T> item = new KeyValuePair<string, T>(dbModel.Name ?? "default", (T)Activator.CreateInstance(typeof(T), new object[] { dbModel, smuDetails }));
                    ParamModes.Add(item);
                }
            }
            else
            {
                var keyValuePairs = IDefault($"cfg\\{ModeType}.cfg", new T());
                foreach (var item in keyValuePairs)
                    ParamModes.Add(item);
            }
        }


        internal ObservableCollection<KeyValuePair<string, FlowParam>> LoadFlowParam()
        {
            FlowParams.Clear();
            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
            {
                List<ModMasterModel> flows = modService.GetFlowAll(GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
                foreach (var dbModel in flows)
                {
                    List<ModDetailModel> flowDetails = modService.GetDetailByPid(dbModel.Id);
                    KeyValuePair<string, FlowParam> item = new KeyValuePair<string, FlowParam>(dbModel.Name ?? "default", new FlowParam(dbModel, flowDetails));
                    ModDetailModel fn = item.Value.GetParameter(FlowParam.FileNameKey);
                    if (fn != null)
                    {
                        string code = fn.GetValueMD5();
                        SysResourceModel res = resourceService.GetByCode(code);
                        if (res != null)
                        {
                            item.Value.DataBase64 = res.Value ?? string.Empty;
                            Tool.Base64ToFile(item.Value.DataBase64, GlobalSetting.GetInstance().SoftwareConfig.SolutionConfig.SolutionFullName, item.Value.FileName ?? string.Empty);
                        }
                    }
                    FlowParams.Add(item);
                }
            }
            else
            {
                var keyValuePairs = IDefault(FileNameFlowParms, new FlowParam());
                foreach (var item in keyValuePairs)
                    FlowParams.Add(item);

            }
            return FlowParams;
        }



        internal List<SysResourceModel> LoadAllServices()
        {
            return resourceService.GetAllServices(GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
        }


        internal ObservableCollection<KeyValuePair<string, MeasureParam>> LoadMeasureParams()
        {
            MeasureParams.Clear();
            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
            {
                List<MeasureMasterModel> devices = measureService.GetAll(GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
                foreach (var dbModel in devices)
                {
                    KeyValuePair<string, MeasureParam> item = new KeyValuePair<string, MeasureParam>(dbModel.Name ?? "default", new MeasureParam(dbModel));
                    MeasureParams.Add(item);
                }
            }
            return MeasureParams;
        }

        internal int PoiMasterDeleteById(int id)
        {
            return poiService.MasterDeleteById(id);
        }

        internal int ModMasterDeleteById(int id)
        {
           return modService.MasterDeleteById(id);
        }


        internal void Save2DB(FlowParam flowParam)
        {
            string fileName = GlobalSetting.GetInstance().SoftwareConfig.SolutionConfig.SolutionFullName + "\\" + flowParam.FileName;
            flowParam.DataBase64 = Tool.FileToBase64(fileName);
            modService.Save(flowParam);
        }

        internal List<SysDictionaryModel> LoadServiceType()
        {
            return dictionaryService.GetAllServiceType();
        }

        internal int ResourceDeleteById(int id)
        {
            return resourceService.DeleteById(id);
        }

        internal List<MeasureDetailModel> LoadMeasureDetail(int pid)
        {
            return measureService.GetDetailByPid(pid);
        }

        internal List<SysModMasterModel> LoadSysModMaster()
        {
            return sysModService.GetAll(GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
        }

        internal List<ModMasterModel> LoadModMasterByPid(int pid)
        {
            return modService.GetMasterByPid(pid);
        }

        internal int Save(MeasureDetailModel detailModel)
        {
            return measureService.Save(detailModel);
        }

        internal int ModMDetailDeleteById(int id)
        {
           return measureService.DetailDeleteById(id);
        }

        internal int MeasureMasterDeleteById(int id)
        {
            return measureService.MasterDeleteById(id);
        }

        public ObservableCollection<KeyValuePair<string, MeasureParam>> MeasureParams { get; set; }
        public ObservableCollection<KeyValuePair<string, AoiParam>> AoiParams { get; set; }
        public ObservableCollection<KeyValuePair<string, CalibrationParam>> CalibrationParams { get; set; } 
        public ObservableCollection<KeyValuePair<string, PGParam>> PGParams { get; set; }
        public ObservableCollection<KeyValuePair<string, SMUParam>> SMUParams { get; set; }
        public ObservableCollection<KeyValuePair<string, LedReusltParam>> LedReusltParams { get; set; }
        public ObservableCollection<KeyValuePair<string, PoiParam>> PoiParams { get; set; }
        public ObservableCollection<KeyValuePair<string, FlowParam>> FlowParams { get; set; }

        public ObservableCollection<KeyValuePair<string, MTFParam>> MTFParams { get; set; }
        public ObservableCollection<KeyValuePair<string, SFRParam>> SFRParams { get; set; }




    }
}
