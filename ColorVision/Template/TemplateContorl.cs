#pragma warning disable CS8604
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.Template.Algorithm;
using ColorVision.Util;
using cvColorVision.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace ColorVision.Template
{
    public enum TemplateType
    {
        AoiParam,
        Calibration,
        PGParam,
        LedReuslt,
        SMUParam,
        PoiParam,
        FlowParam,
        MeasureParm,
        MTFParam,
        SFRParam,
        FOVParam,
        GhostParam,
        DistortionParam
    }

    public class TemplateTypeFactory 
    { 
        public static TemplateType GetWindowTemplateType(string code)
        {
            return code switch
            {
                ModMasterType.Aoi => TemplateType.AoiParam,
                ModMasterType.PG => TemplateType.PGParam,
                ModMasterType.SMU => TemplateType.SMUParam,
                ModMasterType.MTF => TemplateType.MTFParam,
                ModMasterType.SFR => TemplateType.SFRParam,
                ModMasterType.FOV => TemplateType.FOVParam,
                ModMasterType.Ghost => TemplateType.GhostParam,
                ModMasterType.Distortion => TemplateType.DistortionParam,
                _ => TemplateType.AoiParam,
            };
        }

        public static string GetModeTemplateType(TemplateType windowTemplateType)
        {
            return windowTemplateType switch
            {
                TemplateType.AoiParam => ModMasterType.Aoi,
                TemplateType.PGParam => ModMasterType.PG,
                TemplateType.SMUParam => ModMasterType.SMU,
                TemplateType.MTFParam => ModMasterType.MTF,
                TemplateType.SFRParam => ModMasterType.SFR,
                TemplateType.FOVParam => ModMasterType.FOV,
                TemplateType.GhostParam => ModMasterType.Ghost,
                TemplateType.DistortionParam => ModMasterType.Distortion,
                _ => string.Empty,
            };
        }

        public static ParamBase CreateParam(TemplateType windowTemplateType)
        {
            return windowTemplateType switch
            {
                TemplateType.AoiParam => new AoiParam(),
                TemplateType.Calibration => new CalibrationParam(),
                TemplateType.PGParam => new PGParam(),
                TemplateType.LedReuslt => new LedReusltParam(),
                TemplateType.SMUParam => new SMUParam(),
                TemplateType.PoiParam => new PoiParam(),
                TemplateType.FlowParam => new FlowParam(),
                TemplateType.MeasureParm => new MeasureParam(),
                TemplateType.MTFParam => new MTFParam(),
                TemplateType.SFRParam => new SFRParam(),
                TemplateType.FOVParam => new FOVParam(),
                TemplateType.GhostParam => new GhostParam(),
                TemplateType.DistortionParam => new DistortionParam(),
                _ => new ParamBase(),
            };
        }
        public static ParamBase CreateModeParam(TemplateType windowTemplateType, ModMasterModel  modMasterModel, List<ModDetailModel>  modDetailModels)
        {
            return windowTemplateType switch
            {
                TemplateType.AoiParam => new AoiParam(modMasterModel, modDetailModels),
                TemplateType.Calibration => new CalibrationParam(modMasterModel, modDetailModels),
                TemplateType.PGParam => new PGParam(modMasterModel, modDetailModels),
                TemplateType.LedReuslt => new LedReusltParam(modMasterModel, modDetailModels),
                TemplateType.SMUParam => new SMUParam(modMasterModel, modDetailModels),
                TemplateType.FlowParam => new FlowParam(modMasterModel, modDetailModels),
                TemplateType.MTFParam => new MTFParam(modMasterModel, modDetailModels),
                TemplateType.SFRParam => new SFRParam(modMasterModel, modDetailModels),
                TemplateType.FOVParam => new FOVParam(modMasterModel, modDetailModels),
                TemplateType.GhostParam => new GhostParam(modMasterModel, modDetailModels),
                TemplateType.DistortionParam => new DistortionParam(modMasterModel, modDetailModels),
                _ => new ParamBase(),
            };
        }
    }



    /// <summary>
    /// 模板管理
    /// </summary>

    public class TemplateControl
    {
        private static TemplateControl _instance;
        private static readonly object _locker = new();
        public static TemplateControl GetInstance() { lock (_locker) { return _instance ??= new TemplateControl(); } }

        private static string FileNameCalibrationParams = "cfg\\CalibrationSetup.cfg";
        private static string FileNameLedJudgeParams = "cfg\\LedJudgeSetup.cfg";

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
            FOVParams = new ObservableCollection<KeyValuePair<string, FOVParam>>();
            GhostParams = new ObservableCollection<KeyValuePair<string, GhostParam>>();
            DistortionParams = new ObservableCollection<KeyValuePair<string, DistortionParam>>();
            

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

            DicTemplate.TryAdd("Poi", PoiParams);
            DicTemplate.TryAdd("Flow", FlowParams);
            DicTemplate.TryAdd("Calibration", CalibrationParams);
            DicTemplate.TryAdd("LedReuslt", LedReusltParams);
            LoadModParam(AoiParams, ModMasterType.Aoi);
            LoadModParam(SMUParams, ModMasterType.SMU);
            LoadModParam(PGParams, ModMasterType.PG);
            LoadModParam(SFRParams, ModMasterType.SFR);
            LoadModParam(MTFParams, ModMasterType.MTF);
            LoadModParam(FOVParams, ModMasterType.FOV);
            LoadModParam(GhostParams, ModMasterType.Ghost);
            LoadModParam(DistortionParams, ModMasterType.Distortion);
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

        private Dictionary<string, object> DicTemplate = new Dictionary<string, object>();

        public void CSVSave()
        {
            foreach (var item in DicTemplate)
                CfgFile.Save(item.Key, item.Value);
        }


        public void Save(TemplateType windowTemplateType)
        {
            switch (windowTemplateType)
            {
                case TemplateType.Calibration:
                    SaveDefault(FileNameCalibrationParams, CalibrationParams);
                    break;
                case TemplateType.LedReuslt:
                    SaveDefault(FileNameLedJudgeParams, LedReusltParams);
                    break;
                case TemplateType.AoiParam:
                    Save(AoiParams, ModMasterType.Aoi);
                    break;
                case TemplateType.PGParam:
                    Save(PGParams, ModMasterType.PG);
                    break;
                case TemplateType.SMUParam:
                    Save(SMUParams, ModMasterType.SMU);
                    break;
                case TemplateType.MTFParam:
                    Save(MTFParams, ModMasterType.MTF);
                    break;
                case TemplateType.SFRParam:
                    Save(SFRParams, ModMasterType.SFR);
                    break;
                case TemplateType.FOVParam:
                    Save(FOVParams, ModMasterType.FOV);
                    break;
                case TemplateType.GhostParam:
                    Save(GhostParams, ModMasterType.Ghost);
                    break;
                case TemplateType.DistortionParam:
                    Save(DistortionParams, ModMasterType.Distortion);
                    break;
                case TemplateType.PoiParam:
                    SaveDefault(FileNamePoiParms, PoiParams);
                    break;
                case TemplateType.FlowParam:
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
            DicTemplate.TryAdd(ModeType, AoiParams);
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

        public ObservableCollection<KeyValuePair<string, FOVParam>> FOVParams { get; set; }

        public ObservableCollection<KeyValuePair<string,GhostParam>> GhostParams { get; set; }
        public ObservableCollection<KeyValuePair<string, DistortionParam>> DistortionParams { get; set; }




    }
}
