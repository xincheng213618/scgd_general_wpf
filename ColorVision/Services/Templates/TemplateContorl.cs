#pragma warning disable CS8604
using ColorVision.MySql;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Algorithm.Templates;
using ColorVision.Services.Devices.PG.Templates;
using ColorVision.Services.Devices.Sensor.Templates;
using ColorVision.Services.Devices.SMU;
using ColorVision.Services.Flow;
using ColorVision.Services.Templates.POI;
using ColorVision.Settings;
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
        private static void Init()
        {
            PoiParam.LoadPoiParam();
            FlowParam.LoadFlowParam();

            LoadModParam(SMUParam.Params, ModMasterType.SMU);
            LoadModParam(PGParam.Params, ModMasterType.PG);
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


        public static void Save2DB<T>(ObservableCollection<TemplateModel<T>> keyValuePairs) where T : ParamBase
        {
            foreach (var item in keyValuePairs)
            {
                Save2DB(item.Value);
            }
        }

        public static void Save2DB<T>(T value) where T : ParamBase
        {
            if (ModMasterDao.Instance.GetById(value.Id) is ModMasterModel modMasterModel && modMasterModel.Pcode != null)
            {
                modMasterModel.Name = value.Name;
                ModMasterDao modMasterDao = new ModMasterDao(modMasterModel.Pcode);
                modMasterDao.Save(modMasterModel);
            }
            List<ModDetailModel> list = new List<ModDetailModel>();
            value.GetDetail(list);
            ModDetailDao.Instance.UpdateByPid(value.Id, list);
        }



        public static T? AddParamMode<T>(string code, string Name) where T : ParamBase, new()
        {
            ModMasterModel modMaster = new ModMasterModel(code, Name, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            Save(modMaster);
            int pkId = modMaster.Id;
            if (pkId > 0)
            {
                ModMasterModel modMasterModel = ModMasterDao.Instance.GetById(pkId);
                List<ModDetailModel> modDetailModels = ModDetailDao.Instance.GetAllByPid(pkId);
                if (modMasterModel != null) return (T)Activator.CreateInstance(typeof(T), new object[] { modMasterModel, modDetailModels });
                else return null;
            }
            return null;
        }




        public static void LoadModParam<T>(ObservableCollection<TemplateModel<T>> ParamModes, string ModeType) where T : ParamBase, new()
        {
            ParamModes.Clear();
            if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
            {
                ModMasterDao masterFlowDao = new ModMasterDao(ModeType);

                List<ModMasterModel> smus = masterFlowDao.GetAll(ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
                foreach (var dbModel in smus)
                {
                    List<ModDetailModel> smuDetails = ModDetailDao.Instance.GetAllByPid(dbModel.Id);
                    foreach (var dbDetail in smuDetails)
                    {
                        dbDetail.ValueA = dbDetail?.ValueA?.Replace("\\r", "\r");
                    }
                    ParamModes.Add(new TemplateModel<T>(dbModel.Name ?? "default", (T)Activator.CreateInstance(typeof(T), new object[] { dbModel, smuDetails })));
                }
            }
        }

        public static T? AddCalibrationParam<T>(string code, string Name, int resourceId) where T : ParamBase, new()
        {
            ModMasterModel modMaster = new ModMasterModel(code, Name, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            modMaster.ResourceId = resourceId;
            Save(modMaster);
            int pkId = modMaster.Id;
            if (pkId > 0)
            {
                ModMasterModel modMasterModel = ModMasterDao.Instance.GetById(pkId);
                List<ModDetailModel> modDetailModels = ModDetailDao.Instance.GetAllByPid(pkId);
                if (modMasterModel != null) return (T)Activator.CreateInstance(typeof(T), new object[] { modMasterModel, modDetailModels });
                else return null;
            }
            return null;
        }

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


        public ObservableCollection<TemplateModel<AOIParam>> AoiParams { get; set; }


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
