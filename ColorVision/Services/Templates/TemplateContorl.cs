#pragma warning disable CS8604
using ColorVision.Common.Extension;
using ColorVision.Common.Sorts;
using ColorVision.Common.Utilities;
using ColorVision.MySql;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Algorithm.Templates;
using ColorVision.Services.Devices.PG.Templates;
using ColorVision.Services.Devices.Sensor.Templates;
using ColorVision.Services.Devices.SMU;
using ColorVision.Services.Flow;
using ColorVision.Services.Flow.Dao;
using ColorVision.Services.PhyCameras.Templates;
using ColorVision.Services.Templates.Measure;
using ColorVision.Services.Templates.POI;
using ColorVision.Services.Templates.POI.Dao;
using ColorVision.Settings;
using ColorVision.Solution;
using ColorVision.UserSpace;
using cvColorVision.Util;
using CVCommCore;
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
            SMUParams = new ObservableCollection<TemplateModel<SMUParam>>();
            FlowParams = new ObservableCollection<TemplateModel<FlowParam>>();
            MeasureParams = new ObservableCollection<TemplateModel<MeasureParam>>();

            ConfigHandler.GetInstance().SoftwareConfig.UseMySqlChanged += (s) =>
            {
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

            Init();

            Application.Current.MainWindow.Closed += (s, e) =>
            {
                if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
                    return;
            };
        }
        private void Init()
        {
            LoadParams(FlowParams);
            LoadParams(AoiParams);
            LoadParams(SMUParams);
            LoadParams(PGParams);


            LoadParams(LedReusltParam.LedReusltParams);
            LoadModParam(LedCheckParam.LedCheckParams, ModMasterType.LedCheck);

            LoadModParam(FocusPointsParam.FocusPointsParams, ModMasterType.FocusPoints);
            LoadModParam(PoiParam.Params, ModMasterType.POI);
            LoadModParam(SFRParam.SFRParams, ModMasterType.SFR);
            LoadModParam(MTFParam.MTFParams, ModMasterType.MTF);
            LoadModParam(FOVParam.FOVParams, ModMasterType.FOV);
            LoadModParam(GhostParam.GhostParams, ModMasterType.Ghost);
            LoadModParam(DistortionParam.DistortionParams, ModMasterType.Distortion);
            LoadModParam(BuildPOIParam.BuildPOIParams, ModMasterType.BuildPOI);
            LoadModParam(SensorHeYuan.SensorHeYuans, "Sensor.HeYuan");
            LoadModParam(CameraExposureParam.CameraExposureParams, "camera_exp_time");

        }
        public void LoadParams<T>(ObservableCollection<TemplateModel<T>> TemplateModels) where T : ParamBase, new()
        {
            switch (typeof(T))
            {
                case System.Type t when t == typeof(CalibrationParam):
                    break;
                case System.Type t when t == typeof(PoiParam):
                    LoadPoiParam();
                    break;
                case System.Type t when t == typeof(FlowParam):
                    LoadFlowParam();
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
                    LoadModParam(SFRParam.SFRParams, ModMasterType.SFR);
                    break;
                case System.Type t when t == typeof(MTFParam):
                    LoadModParam(MTFParam.MTFParams, ModMasterType.MTF);
                    break;
                case System.Type t when t == typeof(FOVParam):
                    LoadModParam(FOVParam.FOVParams, ModMasterType.FOV);
                    break;
                case System.Type t when t == typeof(GhostParam):
                    LoadModParam(GhostParam.GhostParams, ModMasterType.Ghost);
                    break;
                case System.Type t when t == typeof(DistortionParam):
                    LoadModParam(DistortionParam.DistortionParams, ModMasterType.Distortion);
                    break;
                case System.Type t when t == typeof(FocusPointsParam):
                    LoadModParam(FocusPointsParam.FocusPointsParams, ModMasterType.FocusPoints);
                    break;
                case System.Type t when t == typeof(LedCheckParam):
                    LoadModParam(LedCheckParam.LedCheckParams, ModMasterType.LedCheck);
                    break;
                case System.Type t when t == typeof(MeasureParam):
                    LoadMeasureParams();
                    break;
                case System.Type t when t == typeof(BuildPOIParam):
                    LoadModParam(BuildPOIParam.BuildPOIParams, ModMasterType.LedCheck);
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

        public void Save(TemplateType windowTemplateType)
        {
            switch (windowTemplateType)
            {
                case TemplateType.Calibration:
                    break;
                case TemplateType.PoiParam:
                    foreach (var item in PoiParam.Params)
                    {
                        var modMasterModel = PoiMasterDao.Instance.GetById(item.Id);
                        if (modMasterModel != null)
                        {
                            modMasterModel.Name = item.Key;
                            PoiMasterDao.Instance.Save(modMasterModel);
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
                    Save(MTFParam.MTFParams, ModMasterType.MTF);
                    break;
                case TemplateType.SFRParam:
                    Save(SFRParam.SFRParams, ModMasterType.SFR);
                    break;
                case TemplateType.FOVParam:
                    Save(FOVParam.FOVParams, ModMasterType.FOV);
                    break;
                case TemplateType.GhostParam:
                    Save(GhostParam.GhostParams, ModMasterType.Ghost);
                    break;
                case TemplateType.DistortionParam:
                    Save(DistortionParam.DistortionParams, ModMasterType.Distortion);
                    break;
                case TemplateType.FocusPointsParam:
                    Save(FocusPointsParam.FocusPointsParams, ModMasterType.FocusPoints);
                    break;
                case TemplateType.LedCheckParam:
                    Save(LedCheckParam.LedCheckParams, ModMasterType.LedCheck);
                    break;
                case TemplateType.BuildPOIParmam:
                    Save(BuildPOIParam.BuildPOIParams, ModMasterType.BuildPOI);
                    break;
                case TemplateType.SensorHeYuan:
                    Save(SensorHeYuan.SensorHeYuans, ModMasterType.SensorHeYuan);
                    break;
                case TemplateType.CameraExposureParam:
                    Save(CameraExposureParam.CameraExposureParams, ModMasterType.CameraExposure);
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
            PoiMasterDao.Instance.Save(poiMasterModel);

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


        private PoiDetailDao poiDetail = new PoiDetailDao();

        public static ObservableCollection<TemplateModel<PoiParam>> LoadPoiParam()
        {
            PoiParam.Params.Clear();
            List<PoiMasterModel> poiMasters = PoiMasterDao.Instance.GetAllByTenantId(ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            foreach (var dbModel in poiMasters)
            {
                PoiParam.Params.Add(new TemplateModel<PoiParam>(dbModel.Name ?? "default", new PoiParam(dbModel)));
            }
            return PoiParam.Params;
        }




        internal void LoadPoiDetailFromDB(PoiParam poiParam)
        {
            poiParam.PoiPoints.Clear();
            List<PoiDetailModel> poiDetails = poiDetail.GetAllByPid(poiParam.Id);
            foreach (var dbModel in poiDetails)
            {
                poiParam.PoiPoints.AddUnique(new PoiParamData(dbModel));
            }
        }

        private ModMasterDao masterModDao = new ModMasterDao();
        private ModDetailDao detailDao = new ModDetailDao();
        public T? AddParamMode<T>(string code, string Name) where T : ParamBase, new()
        {
            ModMasterModel modMaster = new ModMasterModel(code, Name, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            Save(modMaster);
            int pkId = modMaster.Id;
            if (pkId > 0)
            {
                ModMasterModel modMasterModel = masterModDao.GetById(pkId);
                List<ModDetailModel> modDetailModels = detailDao.GetAllByPid(pkId);
                if (modMasterModel != null) return (T)Activator.CreateInstance(typeof(T), new object[] { modMasterModel, modDetailModels });
                else return null;
            }
            return null;
        }

        private VSysResourceDao resourceDao = new VSysResourceDao();

        public int Save(ModMasterModel modMaster)
        {
            int ret = -1;
            SysDictionaryModModel mod = SysDictionaryModDao.Instance.GetByCode(modMaster.Pcode, modMaster.TenantId);
            if (mod != null)
            {
                modMaster.Pid = mod.Id;
                ret = masterFlowDao.Save(modMaster);
                List<ModDetailModel> list = new List<ModDetailModel>();
                List<SysDictionaryModDetaiModel> sysDic = SysDictionaryModDetailDao.Instance.GetAllByPid(modMaster.Pid);
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
                        res.Type = (int)PhysicalResourceType.FlowFile;
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
                    res.Type = (int)PhysicalResourceType.FlowFile;
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
            int ret = -1;
            SysDictionaryModModel mod = sysDicDao.GetByCode(modMaster.Pcode, modMaster.TenantId);
            if (mod != null)
            {
                modMaster.Pid = mod.Id;
                ret = modMasterDao.Save(modMaster);
                List<ModDetailModel> list = new List<ModDetailModel>();
                List<SysDictionaryModDetaiModel> sysDic = SysDictionaryModDetailDao.Instance.GetAllByPid(modMaster.Pid);
                foreach (var item in sysDic)
                {
                    list.Add(new ModDetailModel(item.Id, modMaster.Id, item.DefaultValue));
                }
                ModDetailDao.Instance.SaveByPid(modMaster.Id, list);
            }
            return ret;
        }

        private ModFlowDetailDao detailFlowDao = new ModFlowDetailDao();

        internal FlowParam? AddFlowParam(string text)
        {
            ModMasterModel flowMaster = new ModMasterModel(ModMasterType.Flow, text, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            Save(flowMaster);
            int pkId = flowMaster.Id;
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


        internal ResourceParam? AddDeviceParam(string name, string code, int type, int pid)
        {
            SysResourceModel sysResource = new SysResourceModel(name, code, type, pid, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            VSysResourceDao.Instance.Save(sysResource);
            int pkId = sysResource.Id;
            if (pkId > 0)
            {
                return LoadServiceParamById(pkId);
            }
            return null;
        }

        internal ResourceParam? AddServiceParam(string name, string code, int type)
        {
            SysResourceModel sysResource = new SysResourceModel(name, code, type, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            VSysResourceDao.Instance.Save(sysResource);
            int pkId = sysResource.Id;
            if (pkId > 0)
            {
                return LoadServiceParamById(pkId);
            }
            return null;
        }

        internal MeasureParam? AddMeasureParam(string name)
        {
            MeasureMasterModel model = new MeasureMasterModel(name, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            MeasureMasterDao.Instance.Save(model);
            int pkId = model.Id;
            if (pkId > 0)
            {
                return LoadMeasureParamById(pkId);
            }
            return null;
        }
        private MeasureParam? LoadMeasureParamById(int pkId)
        {
            MeasureMasterModel model = MeasureMasterDao.Instance.GetById(pkId);
            if (model != null) return new MeasureParam(model);
            else return null;
        }

        private ResourceParam? LoadServiceParamById(int pkId)
        {
            SysResourceModel model = VSysResourceDao.Instance.GetById(pkId);
            if (model != null) return new ResourceParam(model);
            else return null;
        }

        private void LoadModParam<T>(ObservableCollection<TemplateModel<T>> ParamModes, string ModeType) where T : ParamBase, new()
        {
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
            int pkId = modMaster.Id;
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

        private MeasureDetailDao measureDetail = new MeasureDetailDao();

        internal ObservableCollection<TemplateModel<MeasureParam>> LoadMeasureParams()
        {
            MeasureParams.Clear();
            if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
            {
                List<MeasureMasterModel> devices = MeasureMasterDao.Instance.GetAllByTenantId(ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
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

        public ObservableCollection<TemplateModel<FlowParam>> FlowParams { get; set; }

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
