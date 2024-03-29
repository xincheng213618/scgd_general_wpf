﻿#pragma warning disable CS8604
using ColorVision.Common.Utilities;
using ColorVision.MySql;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Algorithm.Templates;
using ColorVision.Services.Devices.Calibration.Templates;
using ColorVision.Services.Devices.PG.Templates;
using ColorVision.Services.Devices.SMU;
using ColorVision.Services.Flow;
using ColorVision.Services.Flow.Dao;
using ColorVision.Services.Templates.Measure;
using ColorVision.Services.Templates.POI;
using ColorVision.Services.Templates.POI.Dao;
using ColorVision.Settings;
using ColorVision.Solution;
using ColorVision.UserSpace;
using cvColorVision.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;

namespace ColorVision.Services.Templates
{
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

        public string TemplatePath { get; set; }


        public TemplateControl()
        {
            AoiParams = new ObservableCollection<TemplateModel<AOIParam>>();
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

            BuildPOIParams = new ObservableCollection<TemplateModel<BuildPOIParam>>();

            ConfigHandler.GetInstance().SoftwareConfig.UseMySqlChanged += (s) =>
            {
                if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
                    CSVSave();

                Thread thread = new Thread(async () =>
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
                if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
                    return;
                CSVSave();
            };

            SolutionManager.GetInstance().SolutionLoaded += (s, e) =>
            {
                TemplatePath = SolutionManager.GetInstance().SolutionDirectory.FullName;
                if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
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
            LoadParams(BuildPOIParams);
        }
        public void LoadParams<T>(ObservableCollection<TemplateModel<T>> TemplateModels) where T : ParamBase, new()
        {
            switch (typeof(T))
            {
                case System.Type t when t == typeof(CalibrationParam):
                    break;
                case System.Type t when t == typeof(LedReusltParam):
                    IDefault(FileNameLedJudgeParams, new LedReusltParam());
                    DicTemplate.TryAdd("LedReuslt", LedReusltParams);
                    break;
                case System.Type t when t == typeof(PoiParam):
                    LoadPoiParam();
                    DicTemplate.TryAdd("Poi", PoiParams);
                    break;
                case System.Type t when t == typeof(FlowParam):
                    LoadFlowParam();
                    DicTemplate.TryAdd("Flow", FlowParams);
                    break;
                case System.Type t when t == typeof(AOIParam):
                    LoadModParam(AoiParams, ModMasterType.Aoi);
                    break;
                case System.Type t when t == typeof(SMUParam):
                    LoadModParam(SMUParams, ModMasterType.SMU);
                    break;
                case System.Type t when t == typeof(PGParam):
                    LoadModParam(PGParams, ModMasterType.PG);
                    break;
                case System.Type t when t == typeof(SFRParam):
                    LoadModParam(SFRParams, ModMasterType.SFR);
                    break;
                case System.Type t when t == typeof(MTFParam):
                    LoadModParam(MTFParams, ModMasterType.MTF);
                    break;
                case System.Type t when t == typeof(FOVParam):
                    LoadModParam(FOVParams, ModMasterType.FOV);
                    break;
                case System.Type t when t == typeof(GhostParam):
                    LoadModParam(GhostParams, ModMasterType.Ghost);
                    break;
                case System.Type t when t == typeof(DistortionParam):
                    LoadModParam(DistortionParams, ModMasterType.Distortion);
                    break;
                case System.Type t when t == typeof(FocusPointsParam):
                    LoadModParam(FocusPointsParams, ModMasterType.FocusPoints);
                    break;
                case System.Type t when t == typeof(LedCheckParam):
                    LoadModParam(LedCheckParams, ModMasterType.LedCheck);
                    break;
                case System.Type t when t == typeof(MeasureParam):
                    LoadMeasureParams();
                    break;
                case System.Type t when t == typeof(BuildPOIParam):
                    LoadModParam(BuildPOIParams, ModMasterType.BuildPOI);
                    break;
                default:
                    break;

            }
        }

        public T? LoadCFG<T>(string cfgFile)
        {
            return CfgFile.Load<T>(TemplatePath + "\\CFG\\" + cfgFile + ".cfg");
        }



        /// 这里是初始化模板的封装，因为模板的代码高度统一，所以使用泛型T来设置具体的模板参数。
        /// 最后在给模板的每一个元素加上一个切换的效果，即当某一个模板启用时，关闭其他已经启用的模板；
        /// 同一类型，只能存在一个启用的模板
        private ObservableCollection<TemplateModel<T>> IDefault<T>(string FileName, T Default) where T : ParamBase
        {
            ObservableCollection<TemplateModel<T>> Params = new ObservableCollection<TemplateModel<T>>();

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
                    CfgFile.Save(SolutionManager.GetInstance().CurrentSolution.FullName + "\\CFG\\" + item.Key + ".cfg", item.Value);
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
                    break;
                case TemplateType.LedResult:
                    SaveDefault(FileNameLedJudgeParams, LedReusltParams);
                    break;
                case TemplateType.PoiParam:
                    foreach (var item in PoiParams)
                    {
                        var modMasterModel = poiMaster.GetById(item.Id);
                        if (modMasterModel != null)
                        {
                            modMasterModel.Name = item.Key;
                            poiMaster.Save(modMasterModel);
                        }
                    }
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
                case TemplateType.BuildPOIParmam:
                    Save(BuildPOIParams, ModMasterType.BuildPOI);
                    break;
                default:
                    break;
            }
        }

        public void Save<T>(ObservableCollection<TemplateModel<T>> t, string code) where T : ParamBase
        {
            if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
                Save2DB(t);
            else
                SaveDefault(code, t);
        }


        public void Save2DB<T>(ObservableCollection<TemplateModel<T>> keyValuePairs) where T : ParamBase
        {
            foreach (var item in keyValuePairs)
            {
                Save2DB(item.Value);
            }
        }



        public void Save2DB<T>(T value) where T : ParamBase
        {
            Save(value);
        }


        public void Save2DB(PoiParam poiParam)
        {
            PoiMasterModel poiMasterModel = new PoiMasterModel(poiParam);
            poiMaster.Save(poiMasterModel);

            List<PoiDetailModel> poiDetails = new List<PoiDetailModel>();
            foreach (PoiParamData pt in poiParam.PoiPoints)
            {
                PoiDetailModel poiDetail = new PoiDetailModel(poiParam.Id, pt);
                poiDetails.Add(poiDetail);
            }
            poiDetail.SaveByPid(poiParam.Id, poiDetails);
        }


        private void SaveDefault<T>(string FileNameParams, ObservableCollection<TemplateModel<T>> t) where T : ParamBase
        {
            CfgFile.Save(TemplatePath + "\\CFG\\" + FileNameParams + ".cfg", t);
        }


        private PoiMasterDao poiMaster = new PoiMasterDao();
        private PoiDetailDao poiDetail = new PoiDetailDao();

        public ObservableCollection<TemplateModel<PoiParam>> LoadPoiParam()
        {
            if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
            {
                PoiParams.Clear();
                List<PoiMasterModel> poiMasters = poiMaster.GetAll(ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
                foreach (var dbModel in poiMasters)
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
            PoiMasterModel poiMasterModel = new PoiMasterModel(TemplateName, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            poiMaster.Save(poiMasterModel);

            int pkId = poiMasterModel.PKId;
            if (pkId > 0)
            {
                PoiMasterModel Service = poiMaster.GetById(pkId);
                if (Service != null) return new PoiParam(Service);
                else return null;
            }
            return null;
        }


        internal void LoadPoiDetailFromDB(PoiParam poiParam)
        {
            poiParam.PoiPoints.Clear();
            List<PoiDetailModel> poiDetails = poiDetail.GetAllByPid(poiParam.Id);
            foreach (var dbModel in poiDetails)
            {
                poiParam.PoiPoints.Add(new PoiParamData(dbModel));
            }
        }

        private ModMasterDao masterModDao = new ModMasterDao();
        private ModDetailDao detailDao = new ModDetailDao();
        public T? AddParamMode<T>(string code, string Name) where T : ParamBase, new()
        {
            ModMasterModel modMaster = new ModMasterModel(code, Name, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            Save(modMaster);
            int pkId = modMaster.PKId;
            if (pkId > 0)
            {
                ModMasterModel modMasterModel = masterModDao.GetById(pkId);
                List<ModDetailModel> modDetailModels = detailDao.GetAllByPid(pkId);
                if (modMasterModel != null) return (T)Activator.CreateInstance(typeof(T), new object[] { modMasterModel, modDetailModels });
                else return null;
            }
            return null;
        }

        private SysDictionaryModDetailDao sysDao = new SysDictionaryModDetailDao();
        private SysDictionaryModDao sysDicDao = new SysDictionaryModDao();
        private VSysResourceDao resourceDao = new VSysResourceDao();

        public int Save(ModMasterModel modMaster)
        {
            int ret = -1;
            SysDictionaryModModel mod = sysDicDao.GetByCode(modMaster.Pcode, modMaster.TenantId);
            if (mod != null)
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

        public void Save(FlowParam flowParam)
        {
            List<ModDetailModel> list = new List<ModDetailModel>();
            flowParam.GetDetail(list);
            if (list.Count > 0 && list[0] is ModDetailModel model)
            {
                if (int.TryParse(model.ValueA, out int id))
                {
                    SysResourceModel res = resourceDao.GetById(id);
                    if (res != null)
                    {
                        res.Code = Cryptography.GetMd5Hash(flowParam.DataBase64);
                        res.Name = flowParam.Name;
                        res.Value = flowParam.DataBase64;
                        resourceDao.Save(res);
                    }
                    else
                    {
                        res = new SysResourceModel();
                        res.Name = flowParam.Name;
                        res.Type = 101;
                        if (!string.IsNullOrEmpty(flowParam.DataBase64))
                        {
                            res.Code = Cryptography.GetMd5Hash(flowParam.DataBase64);
                            res.Value = flowParam.DataBase64;
                        }
                        resourceDao.Save(res);
                        model.ValueA = res.Id.ToString();
                    }
                }
                else
                {
                    SysResourceModel res = new SysResourceModel();
                    res.Name = flowParam.Name;
                    res.Type = 101;
                    if (!string.IsNullOrEmpty(flowParam.DataBase64))
                    {
                        res.Code = Cryptography.GetMd5Hash(flowParam.DataBase64);
                        res.Value = flowParam.DataBase64;
                    }
                    resourceDao.Save(res);
                    model.ValueA = res.Id.ToString();
                }
                detailDao.UpdateByPid(flowParam.Id, list);
            }
        }

        public static int Save1(ModMasterModel modMaster)
        {
            ModMasterDao modMasterDao = new ModMasterDao();
            SysDictionaryModDao sysDicDao = new SysDictionaryModDao();
            SysDictionaryModDetailDao sysDao = new SysDictionaryModDetailDao();
            ModDetailDao detailDao = new ModDetailDao();
            int ret = -1;
            SysDictionaryModModel mod = sysDicDao.GetByCode(modMaster.Pcode, modMaster.TenantId);
            if (mod != null)
            {
                modMaster.Pid = mod.Id;
                ret = modMasterDao.Save(modMaster);
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

        private ModFlowDetailDao detailFlowDao = new ModFlowDetailDao();

        internal FlowParam? AddFlowParam(string text)
        {
            ModMasterModel flowMaster = new ModMasterModel(ModMasterType.Flow, text, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            Save(flowMaster);
            int pkId = flowMaster.PKId;
            if (pkId > 0)
            {
                List<ModFlowDetailModel> flowDetail = detailFlowDao.GetAllByPid(pkId);
                if (int.TryParse(flowDetail[0].ValueA, out int id))
                {
                    SysResourceModel sysResourceModeldefault = resourceDao.GetById(id);
                    if (sysResourceModeldefault != null)
                    {
                        SysResourceModel sysResourceModel = new SysResourceModel();
                        sysResourceModel.Name = flowMaster.Name;
                        sysResourceModel.Code = sysResourceModeldefault.Code;
                        sysResourceModel.Type = sysResourceModeldefault.Type;
                        sysResourceModel.Value = sysResourceModeldefault.Value;
                        resourceDao.Save(sysResourceModel);
                        flowDetail[0].ValueA = sysResourceModel.Id.ToString();
                        detailFlowDao.Save(flowDetail[0]);
                    }
                }
                if (flowMaster != null) return new FlowParam(flowMaster, flowDetail);
                else return null;
            }
            return null;
        }

        private VSysResourceDao VSysResourceDao { get; set; } = new VSysResourceDao();

        internal ResourceParam? AddDeviceParam(string name, string code, int type, int pid)
        {
            SysResourceModel sysResource = new SysResourceModel(name, code, type, pid, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            VSysResourceDao.Save(sysResource);
            int pkId = sysResource.PKId;
            if (pkId > 0)
            {
                return LoadServiceParamById(pkId);
            }
            return null;
        }

        internal ResourceParam? AddServiceParam(string name, string code, int type)
        {
            SysResourceModel sysResource = new SysResourceModel(name, code, type, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            VSysResourceDao.Save(sysResource);
            int pkId = sysResource.PKId;
            if (pkId > 0)
            {
                return LoadServiceParamById(pkId);
            }
            return null;
        }

        internal MeasureParam? AddMeasureParam(string name)
        {
            MeasureMasterModel model = new MeasureMasterModel(name, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            measureMaster.Save(model);
            int pkId = model.PKId;
            if (pkId > 0)
            {
                return LoadMeasureParamById(pkId);
            }
            return null;
        }
        private MeasureParam? LoadMeasureParamById(int pkId)
        {
            MeasureMasterModel model = measureMaster.GetById(pkId);
            if (model != null) return new MeasureParam(model);
            else return null;
        }
        private ResourceParam? LoadServiceParamById(int pkId)
        {
            SysResourceModel model = VSysResourceDao.GetById(pkId);
            if (model != null) return new ResourceParam(model);
            else return null;
        }

        private void LoadModParam<T>(ObservableCollection<TemplateModel<T>> ParamModes, string ModeType) where T : ParamBase, new()
        {
            DicTemplate.TryAdd(ModeType, ParamModes);
            ParamModes.Clear();
            if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
            {
                ModMasterDao masterFlowDao = new ModMasterDao(ModeType);

                List<ModMasterModel> smus = masterFlowDao.GetAll(ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
                foreach (var dbModel in smus)
                {
                    List<ModDetailModel> smuDetails = detailDao.GetAllByPid(dbModel.Id);
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

        public void LoadModCabParam<T>(ObservableCollection<TemplateModel<T>> CalibrationParamModes, int resourceId, string ModeType) where T : ParamBase, new()
        {
            DicTemplate.TryAdd(ModeType, CalibrationParamModes);
            CalibrationParamModes.Clear();
            if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
            {
                ModMasterDao masterFlowDao = new ModMasterDao(ModeType);
                List<ModMasterModel> smus = masterFlowDao.GetResourceAll(ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId, resourceId);
                foreach (var dbModel in smus)
                {
                    List<ModDetailModel> smuDetails = detailDao.GetAllByPid(dbModel.Id);
                    foreach (var dbDetail in smuDetails)
                    {
                        dbDetail.ValueA = dbDetail?.ValueA?.Replace("\\r", "\r");
                    }
                    CalibrationParamModes.Add(new TemplateModel<T>(dbModel.Name ?? "default", (T)Activator.CreateInstance(typeof(T), new object[] { dbModel, smuDetails })));
                }
            }
        }
        internal void Save(ParamBase value)
        {
            if (masterModDao.GetById(value.Id) is ModMasterModel modMasterModel && modMasterModel.Pcode != null)
            {
                modMasterModel.Name = value.Name;
                ModMasterDao modMasterDao = new ModMasterDao(modMasterModel.Pcode);
                modMasterDao.Save(modMasterModel);
            }
            List<ModDetailModel> list = new List<ModDetailModel>();
            value.GetDetail(list);
            detailDao.UpdateByPid(value.Id, list);
        }

        public T? AddCalibrationParam<T>(string code, string Name, int resourceId) where T : ParamBase, new()
        {
            ModMasterModel modMaster = new ModMasterModel(code, Name, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            modMaster.ResourceId = resourceId;
            Save(modMaster);
            int pkId = modMaster.PKId;
            if (pkId > 0)
            {
                ModMasterModel modMasterModel = masterModDao.GetById(pkId);
                List<ModDetailModel> modDetailModels = detailDao.GetAllByPid(pkId);
                if (modMasterModel != null) return (T)Activator.CreateInstance(typeof(T), new object[] { modMasterModel, modDetailModels });
                else return null;
            }
            return null;
        }

        private ModMasterDao masterFlowDao = new ModMasterDao(ModMasterType.Flow);


        internal ObservableCollection<TemplateModel<FlowParam>> LoadFlowParam()
        {
            FlowParams.Clear();
            if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
            {
                List<ModMasterModel> flows = masterFlowDao.GetAll(UserCenter.GetInstance().TenantId);
                foreach (var dbModel in flows)
                {
                    List<ModFlowDetailModel> flowDetails = detailFlowDao.GetAllByPid(dbModel.Id);
                    var item = new TemplateModel<FlowParam>(dbModel.Name ?? "default", new FlowParam(dbModel, flowDetails));
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

        private MeasureMasterDao measureMaster = new MeasureMasterDao();
        private MeasureDetailDao measureDetail = new MeasureDetailDao();

        internal ObservableCollection<TemplateModel<MeasureParam>> LoadMeasureParams()
        {
            MeasureParams.Clear();
            if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
            {
                List<MeasureMasterModel> devices = measureMaster.GetAll(ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
                foreach (var dbModel in devices)
                {
                    MeasureParams.Add(new TemplateModel<MeasureParam>(dbModel.Name ?? "default", new MeasureParam(dbModel)));
                }
            }
            return MeasureParams;
        }


        internal void Save2DB(FlowParam flowParam)
        {
            Save(flowParam);
        }


        public ObservableCollection<TemplateModel<MeasureParam>> MeasureParams { get; set; }
        public ObservableCollection<TemplateModel<AOIParam>> AoiParams { get; set; }
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

        public ObservableCollection<TemplateModel<BuildPOIParam>> BuildPOIParams { get; set; }

        public static ObservableCollection<TemplateModelBase> GetTemplateModelBases<T>(ObservableCollection<TemplateModel<T>> templateModels) where T : ParamBase
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
