﻿#pragma warning disable CS8604
using ColorVision.Flow.Templates;
using ColorVision.MySql;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.Services.Algorithm.Templates;
using ColorVision.Services.Device.PG.Templates;
using ColorVision.Solution;
using ColorVision.Templates.POI.MySql;
using ColorVision.User;
using ColorVision.Util;
using cvColorVision.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;

namespace ColorVision.Templates
{

    public class PoiControl
    {
        private static PoiControl _instance;
        private static readonly object _locker = new();
        public static PoiControl GetInstance() { lock (_locker) { return _instance ??= new PoiControl(); } }
        public ObservableCollection<TemplateModel<PoiParam>> PoiParams { get; set; }

        public PoiControl()
        {

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

        private static string FileNameLedJudgeParams = "LedJudgeSetup";
        private static string FileNameFlowParms = "FlowParmSetup";



        private PoiService poiService = new PoiService();
        private ModService modService = new ModService();
        private SysModMasterService sysModService = new SysModMasterService();
        private SysResourceService resourceService = new SysResourceService();
        private SysDictionaryService dictionaryService = new SysDictionaryService();
        private MeasureService measureService = new MeasureService();

        public string TemplatePath { get; set; }


        public TemplateControl()
        {
            AoiParams = new ObservableCollection<TemplateModel<AOIParam>>();
            CalibrationParams = new ObservableCollection<TemplateModel<CalibrationParam>>();
            PGParams = new ObservableCollection<TemplateModel<PGParam>>();
            LedReusltParams = new ObservableCollection<TemplateModel<LedReusltParam>>();
            SMUParams = new ObservableCollection<TemplateModel<SMUParam>>();
            FlowParams = new ObservableCollection<TemplateModel<FlowParam>>();
            PoiParams = new ObservableCollection<TemplateModel<PoiParam>>();
            PoiParam.Params = PoiParams;
            MeasureParams = new ObservableCollection<TemplateModel<MeasureParam>>();
            MTFParams = new ObservableCollection<TemplateModel<MTFParam>>();
            SFRParams = new ObservableCollection<TemplateModel<SFRParam>>();
            FOVParams = new ObservableCollection<TemplateModel<FOVParam>>();
            GhostParams = new ObservableCollection<TemplateModel<GhostParam>>();
            DistortionParams = new ObservableCollection<TemplateModel<DistortionParam>>();
            LedCheckParams = new ObservableCollection<TemplateModel<LedCheckParam>>();
            FocusPointsParams = new ObservableCollection<TemplateModel<FocusPointsParam>>();
            

            GlobalSetting.GetInstance().SoftwareConfig.UseMySqlChanged += (s) =>
            {
                if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
                    CSVSave();

                Thread  thread  = new Thread(async () =>
                {
                    if (!MySqlControl.GetInstance().IsConnect)
                        await MySqlControl.GetInstance().Connect();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Init();
                    });
                });
                thread.Start();

            };

            TemplatePath = SolutionManager.GetInstance().SolutionDirectory.FullName;
            Init();



            Application.Current.MainWindow.Closed += (s, e) =>
            {
                if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
                    return;
                CSVSave();
            };

            SolutionManager.GetInstance().SolutionLoaded += (s, e) =>
            {
                TemplatePath = SolutionManager.GetInstance().SolutionDirectory.FullName;
                if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
                {
                    LoadFlowParam();
                }
                else
                {
                    CSVSave();
                }
            };
        }
        private void Init()
        {
            LoadParams(LedReusltParams);
            LoadParams(PoiParams);
            LoadParams(FlowParams);
            LoadParams(CalibrationParams);
            LoadParams(AoiParams);
            LoadParams(SMUParams);
            LoadParams(PGParams);
            LoadParams(SFRParams);
            LoadParams(MTFParams);
            LoadParams(FOVParams);
            LoadParams(GhostParams);
            LoadParams(DistortionParams);
            LoadParams(FocusPointsParams);
            LoadParams(LedCheckParams);
        }
        public void LoadParams<T>(ObservableCollection<TemplateModel<T>> TemplateModels) where T : ParamBase, new()
        {
            switch (typeof(T))
            {
                case Type t when t == typeof(CalibrationParam):
                    CalibrationRsourceService.GetInstance().Refresh();
                    LoadModParam(CalibrationParams, ModMasterType.Calibration);
                    break;
                case Type t when t == typeof(LedReusltParam):
                    IDefault(FileNameLedJudgeParams, new LedReusltParam());
                    DicTemplate.TryAdd("LedReuslt", LedReusltParams);
                    break;
                case Type t when t == typeof(PoiParam):
                    LoadPoiParam();
                    DicTemplate.TryAdd("Poi", PoiParams);
                    break;
                case Type t when t == typeof(FlowParam):
                    LoadFlowParam();
                    DicTemplate.TryAdd("Flow", FlowParams);
                    break;
                case Type t when t == typeof(AOIParam):
                    LoadModParam(AoiParams, ModMasterType.Aoi);
                    break;
                case Type t when t == typeof(SMUParam):
                    LoadModParam(SMUParams, ModMasterType.SMU);
                    break;
                case Type t when t == typeof(PGParam):
                    LoadModParam(PGParams, ModMasterType.PG);
                    break;
                case Type t when t == typeof(SFRParam):
                    LoadModParam(SFRParams, ModMasterType.SFR);
                    break;
                case Type t when t == typeof(MTFParam):
                    LoadModParam(MTFParams, ModMasterType.MTF);
                    break;
                case Type t when t == typeof(FOVParam):
                    LoadModParam(FOVParams, ModMasterType.FOV);
                    break;
                case Type t when t == typeof(GhostParam):
                    LoadModParam(GhostParams, ModMasterType.Ghost);
                    break;
                case Type t when t == typeof(DistortionParam):
                    LoadModParam(DistortionParams, ModMasterType.Distortion);
                    break;
                case Type t when t == typeof(FocusPointsParam):
                    LoadModParam(FocusPointsParams, ModMasterType.FocusPoints);
                    break;
                case Type t when t == typeof(LedCheckParam):
                    LoadModParam(LedCheckParams, ModMasterType.LedCheck);
                    break;
                case Type t when t == typeof(MeasureParam):
                    LoadMeasureParams();
                    break;
                default:
                    break;

            }
        }

        public T? LoadCFG<T>(string cfgFile)
        {
            return CfgFile.Load<T>(TemplatePath + "\\CFG\\"+ cfgFile + ".cfg");
        }



        /// 这里是初始化模板的封装，因为模板的代码高度统一，所以使用泛型T来设置具体的模板参数。
        /// 最后在给模板的每一个元素加上一个切换的效果，即当某一个模板启用时，关闭其他已经启用的模板；
        /// 同一类型，只能存在一个启用的模板
        private  ObservableCollection<TemplateModel<T>> IDefault<T>(string FileName ,T Default) where T : ParamBase
        {
            ObservableCollection<TemplateModel<T >> Params = new ObservableCollection<TemplateModel<T>>();

            Params = LoadCFG<ObservableCollection<TemplateModel<T>>>(FileName) ?? new ObservableCollection<TemplateModel<T>>();
            if (Params.Count == 0)
            {
                Params.Add(new TemplateModel<T>("default", Default));
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
            {
                if (Directory.Exists(SolutionManager.GetInstance().CurrentSolution.FullName))
                {
                    CfgFile.Save(SolutionManager.GetInstance().CurrentSolution.FullName + "\\CFG\\" + item.Key +".cfg", item.Value);
                }
                else
                {
                    CfgFile.Save(item.Key, item.Value);

                }
            }
        }


        public void Save(TemplateType windowTemplateType)
        {
            switch (windowTemplateType)
            {
                case TemplateType.Calibration:
                    Save(CalibrationParams, ModMasterType.Calibration);
                    break;
                case TemplateType.LedResult:
                    SaveDefault(FileNameLedJudgeParams, LedReusltParams);
                    break;
                case TemplateType.PoiParam:
                    Save(PoiParams, ModMasterType.POI);

                    if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
                    {
                        foreach (var item in PoiParams)
                        {

                            var modMasterModel = poiService.GetMasterById(item.ID);
                            if (modMasterModel != null)
                            {
                                modMasterModel.Name = item.Key;
                                poiService.Save(modMasterModel);
                            }
                        }
                    }
                    else
                        SaveDefault($"{ModMasterType.POI}", PoiParams);

                    break;
                case TemplateType.FlowParam:
                    Save(FlowParams, ModMasterType.Flow);
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
                case TemplateType.FocusPointsParam:
                    Save(FocusPointsParams, ModMasterType.FocusPoints);
                    break;
                case TemplateType.LedCheckParam:
                    Save(LedCheckParams, ModMasterType.LedCheck);
                    break;
                default:
                    break;
            }
        }

        private void Save<T>(ObservableCollection<TemplateModel<T>> t ,string code) where T: ParamBase
        {
            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
                Save2DB(t);
            else 
                SaveDefault(code, t);
        }


        public void Save2DB<T>(ObservableCollection<TemplateModel<T>>  keyValuePairs) where T : ParamBase
        {
            foreach (var item in keyValuePairs)
            {
                Save2DB(item.Value);
            }
        }



        public void Save2DB<T>(T value) where T : ParamBase
        {
            modService.Save(value);
        }


        public void Save2DB(PoiParam poiParam)
        {
            poiService.Save(poiParam);
        }


        private  void SaveDefault<T>(string FileNameParams, ObservableCollection<TemplateModel<T>> t) where T :ParamBase
        {
            CfgFile.Save(TemplatePath + "\\CFG\\" + FileNameParams + ".cfg", t);
        }



        public ObservableCollection<TemplateModel<PoiParam>> LoadPoiParam()
        {
            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
            {
                PoiParams.Clear();
                List<PoiMasterModel> poiMaster = poiService.GetMasterAll(GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
                foreach (var dbModel in poiMaster)
                {
                    PoiParams.Add(new TemplateModel<PoiParam>(dbModel.Name ?? "default", new PoiParam(dbModel)));
                }
            }
            else
            {
                PoiParams.Clear();
                if (PoiParams.Count == 0)
                    PoiParams = IDefault($"{ModMasterType.POI}.cfg", new PoiParam());
            }

            return PoiParams;
        }

        public PoiParam? AddPoiParam(string TemplateName)
        {
            PoiMasterModel poiMaster = new PoiMasterModel(TemplateName, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
            poiService.Save(poiMaster);

            int pkId = poiMaster.GetPK();
            if (pkId > 0)
            {
                PoiMasterModel Service = poiService.GetMasterById(pkId);
                if (Service != null) return new PoiParam(Service);
                else return null;
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
            ModMasterModel modMaster = new ModMasterModel(code, Name, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
            modService.Save(modMaster);
            int pkId = modMaster.GetPK();
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
                List<ModDetailModel> flowDetail = modService.GetDetailByPid(pkId);
                if (flowMaster != null) return new FlowParam(flowMaster, flowDetail);
                else return null;
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

        private void LoadModParam<T>(ObservableCollection<TemplateModel<T>> ParamModes, string ModeType) where T : ParamBase,new ()
        {
            DicTemplate.TryAdd(ModeType, ParamModes);
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
                    ParamModes.Add(new TemplateModel<T>(dbModel.Name ?? "default", (T)Activator.CreateInstance(typeof(T), new object[] { dbModel, smuDetails })));
                }
            }
            else
            {
                var keyValuePairs = IDefault(ModeType, new T());
                foreach (var item in keyValuePairs)
                    ParamModes.Add(item);
            }
        }


        internal ObservableCollection<TemplateModel<FlowParam>> LoadFlowParam()
        {
            FlowParams.Clear();
            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
            {
                List<ModMasterModel> flows = modService.GetFlowAll(UserCenter.GetInstance().TenantId);
                foreach (var dbModel in flows)
                {
                    List<ModDetailModel> flowDetails = modService.GetDetailByPid(dbModel.Id);
                    var item = new TemplateModel<FlowParam>(dbModel.Name ?? "default", new FlowParam(dbModel, flowDetails));
                    ModDetailModel fn = item.Value.GetParameter(FlowParam.propertyName);
                    if (fn != null)
                    {
                        string code = fn.GetValueMD5();
                        SysResourceModel res = resourceService.GetByCode(code);
                        if (res != null)
                        {
                            item.Value.DataBase64 = res.Value ?? string.Empty;
                            Tool.Base64ToFile(item.Value.DataBase64, SolutionManager.GetInstance().CurrentSolution.FullName +"\\Flow\\", item.Value.FileName ?? string.Empty);
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


        internal ObservableCollection<TemplateModel<MeasureParam>> LoadMeasureParams()
        {
            MeasureParams.Clear();
            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
            {
                List<MeasureMasterModel> devices = measureService.GetAll(GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
                foreach (var dbModel in devices)
                {
                    MeasureParams.Add(new TemplateModel<MeasureParam>(dbModel.Name ?? "default", new MeasureParam(dbModel)));
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

        public ObservableCollection<TemplateModel<MeasureParam>> MeasureParams { get; set; }
        public ObservableCollection<TemplateModel<AOIParam>> AoiParams { get; set; }
        public ObservableCollection<TemplateModel<CalibrationParam>> CalibrationParams { get; set; } 
        public ObservableCollection<TemplateModel<PGParam>> PGParams { get; set; }
        public ObservableCollection<TemplateModel<SMUParam>> SMUParams { get; set; }
        public ObservableCollection<TemplateModel<LedReusltParam>> LedReusltParams { get; set; }
        public ObservableCollection<TemplateModel<PoiParam>> PoiParams { get; set; }
        public ObservableCollection<TemplateModel<FlowParam>> FlowParams { get; set; }
        public ObservableCollection<TemplateModel<MTFParam>> MTFParams { get; set; }
        public ObservableCollection<TemplateModel<SFRParam>> SFRParams { get; set; }
        public ObservableCollection<TemplateModel<FOVParam>> FOVParams { get; set; }
        public ObservableCollection<TemplateModel<GhostParam>> GhostParams { get; set; }
        public ObservableCollection<TemplateModel<DistortionParam>> DistortionParams { get; set; }

        public ObservableCollection<TemplateModel<LedCheckParam>> LedCheckParams { get; set; }
        public ObservableCollection<TemplateModel<FocusPointsParam>> FocusPointsParams { get; set; }

        public static ObservableCollection<TemplateModelBase> GetTemplateModelBases<T>(ObservableCollection<TemplateModel<T>> templateModels) where T:ParamBase
        {
            ObservableCollection<TemplateModelBase> templateModelBases = new ObservableCollection<TemplateModelBase>();
            foreach (var item in templateModels)
            {
                templateModelBases.Add(item);
            }
            return templateModelBases;
        }
    }
}
