#pragma warning disable CS8604
using ColorVision.Common.Utilities;
using ColorVision.MySql;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Algorithm.Templates;
using ColorVision.Services.Devices.PG.Templates;
using ColorVision.Services.Devices.Sensor.Templates;
using ColorVision.Services.Devices.SMU;
using ColorVision.Services.Flow;
using ColorVision.Services.PhyCameras.Templates;
using ColorVision.Services.Templates.Measure;
using ColorVision.Services.Templates.POI;
using ColorVision.Services.Templates.POI.Dao;
using ColorVision.Settings;
using CVCommCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        public TemplateControl()
        {
            AoiParams = new ObservableCollection<TemplateModel<AOIParam>>();
            PGParams = new ObservableCollection<TemplateModel<PGParam>>();
            SMUParams = new ObservableCollection<TemplateModel<SMUParam>>();
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
            LoadParams(AoiParams);
            LoadParams(SMUParams);
            LoadParams(PGParams);


            LoadParams(LedReusltParam.LedReusltParams);

            PoiParam.LoadPoiParam();
            LoadParams(FlowParam.Params);

            LoadModParam(LedCheckParam.LedCheckParams, ModMasterType.LedCheck);
            LoadModParam(FocusPointsParam.FocusPointsParams, ModMasterType.FocusPoints);     
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
                    PoiParam.LoadPoiParam();
                    break;
                case System.Type t when t == typeof(FlowParam):
                    FlowParam.LoadFlowParam();
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

        /// 这里是初始化模板的封装，因为模板的代码高度统一，所以使用泛型T来设置具体的模板参数。
        /// 最后在给模板的每一个元素加上一个切换的效果，即当某一个模板启用时，关闭其他已经启用的模板；
        /// 同一类型，只能存在一个启用的模板
        private static ObservableCollection<TemplateModel<T>> IDefault<T>(string FileName, T Default) where T : ParamBase
        {
            ObservableCollection<TemplateModel<T>> Params = new ObservableCollection<TemplateModel<T>>();

            Params =  new ObservableCollection<TemplateModel<T>>();
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
                    Save2DB(FlowParam.Params);
                    break;
                case TemplateType.AoiParam:
                    Save2DB(AoiParams);
                    break;
                case TemplateType.PGParam:
                    Save2DB(PGParams);
                    break;
                case TemplateType.SMUParam:
                    Save2DB(SMUParams);
                    break;
                case TemplateType.MTFParam:
                    Save2DB(MTFParam.MTFParams);
                    break;
                case TemplateType.SFRParam:
                    Save2DB(SFRParam.SFRParams);
                    break;
                case TemplateType.FOVParam:
                    Save2DB(FOVParam.FOVParams);
                    break;
                case TemplateType.GhostParam:
                    Save2DB(GhostParam.GhostParams);
                    break;
                case TemplateType.DistortionParam:
                    Save2DB(DistortionParam.DistortionParams);
                    break;
                case TemplateType.FocusPointsParam:
                    Save2DB(FocusPointsParam.FocusPointsParams);
                    break;
                case TemplateType.LedCheckParam:
                    Save2DB(LedCheckParam.LedCheckParams);
                    break;
                case TemplateType.BuildPOIParmam:
                    Save2DB(BuildPOIParam.BuildPOIParams);
                    break;
                case TemplateType.SensorHeYuan:
                    Save2DB(SensorHeYuan.SensorHeYuans);
                    break;
                case TemplateType.CameraExposureParam:
                    Save2DB(CameraExposureParam.CameraExposureParams);
                    break;
                default:
                    break;
            }
        }

        public void Save<T>(ObservableCollection<TemplateModel<T>> t) where T : ParamBase
        {
            if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
                Save2DB(t);
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

        public static int Save(ModMasterModel modMaster)
        {
            int ret = -1;
            SysDictionaryModModel mod = SysDictionaryModDao.Instance.GetByCode(modMaster.Pcode, modMaster.TenantId);
            if (mod != null)
            {
                modMaster.Pid = mod.Id;
                ret = ModMasterDao.Instance.Save(modMaster);
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


        internal static MeasureParam? AddMeasureParam(string name)
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
        private static MeasureParam? LoadMeasureParamById(int pkId)
        {
            MeasureMasterModel model = MeasureMasterDao.Instance.GetById(pkId);
            if (model != null) return new MeasureParam(model);
            else return null;
        }

        private static ResourceParam? LoadServiceParamById(int pkId)
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
        private ModMasterDao masterFlowDao = new ModMasterDao(ModMasterType.Flow);

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
