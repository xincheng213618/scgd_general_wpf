using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.SettingUp;
using ColorVision.Util;
using cvColorVision;
using ScottPlot.Styles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms.Design;

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


        private bool IsOldAoiParams;
        private bool IsOldCalibrationParams;
        private bool IsOldPGParams;
        private bool IsOldLedJudgeParams;
        private bool IsOldSxParams;
        private bool IsOldPoiParams;
        private bool IsOldFlowParams;

        private PoiService poiService = new PoiService();
        private ModService modService = new ModService();

        public TemplateControl()
        {
            if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory+ "cfg"))
            {
                Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "cfg");
            }


            AoiParam param = new AoiParam
            {
                FilterByArea = true,
                MaxArea = 6000,
                MinArea = 10,
                FilterByContrast = true,
                MaxContrast = 1.7f,
                MinContrast = 0.3f,
                ContrastBrightness = 1.0f,
                ContrastDarkness = 0.5f,
                BlurSize = 19,
                MinContourSize = 5,
                ErodeSize = 5,
                DilateSize = 5,
                Left = 5,
                Right = 5,
                Top = 5,
                Bottom = 5
            };

            AoiParams = IDefault(FileNameAoiParams, param, ref IsOldAoiParams);
            CalibrationParams = IDefault(FileNameCalibrationParams, new CalibrationParam(),ref IsOldCalibrationParams);
            PGParams = IDefault(FileNamePGParams, new PGParam(), ref IsOldPGParams);

            LedReusltParams = IDefault(FileNameLedJudgeParams, new LedReusltParam(), ref IsOldLedJudgeParams);
            SxParams = IDefault(FileNameSxParms, new SxParam(), ref IsOldSxParams);

            FlowParams = IDefault(FileNameFlowParms, new FlowParam(), ref IsOldFlowParams);



            PoiParams = new ObservableCollection<KeyValuePair<string, PoiParam>>();

            Application.Current.MainWindow.Closed += (s, e) =>
            {
                Save();
            };
        }
        /// 这里是初始化模板的封装，因为模板的代码高度统一，所以使用泛型T来设置具体的模板参数。
        /// 又因为需要兼容之前的代码写法，所以在中间层做了一个转换逻辑，让代码可以读之前的，也可以读现在的，读之前的也保存之前的 <summary>
        /// 这里是初始化模板的封装，因为模板的代码高度统一，所以使用泛型T来设置具体的模板参数。
        /// 最后在给模板的每一个元素加上一个切换的效果，即当某一个模板启用时，关闭其他已经启用的模板；
        /// 同一类型，只能存在一个启用的模板
        private static ObservableCollection<KeyValuePair<string, T>> IDefault<T>(string FileName ,T Default , ref bool IsOldParams) where T : ParamBase
        {
            ObservableCollection<KeyValuePair<string, T>> Params = new ObservableCollection<KeyValuePair<string, T>>();

            Dictionary<string, T> ParamsOld = CfgFile.Load<Dictionary<string, T>>(FileName) ?? new Dictionary<string, T>();
            if (ParamsOld.Count != 0)
            {
                IsOldParams = true;
                Params = new ObservableCollection<KeyValuePair<string, T>>();
                foreach (var item in ParamsOld)
                {
                    Params.Add(item);
                }
            }
            else
            {
                Params = CfgFile.Load<ObservableCollection<KeyValuePair<string, T>>>(FileName) ?? new ObservableCollection<KeyValuePair<string, T>>();
                if (Params.Count == 0)
                {
                    Params.Add(new KeyValuePair<string, T>("default", Default));
                }
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



        public void Save()
        {
            SaveDefault(FileNameAoiParams, AoiParams,IsOldAoiParams);
            SaveDefault(FileNameCalibrationParams, CalibrationParams, IsOldCalibrationParams);
            SaveDefault(FileNamePGParams, PGParams, IsOldPGParams);
            SaveDefault(FileNameLedJudgeParams, LedReusltParams, IsOldLedJudgeParams);
            SaveDefault(FileNameSxParms, SxParams, IsOldSxParams);
            if (!GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
                SaveDefault(FileNamePoiParms, PoiParams, IsOldPoiParams);
            SaveDefault(FileNameFlowParms, FlowParams, IsOldFlowParams);
        }


        public void Save(WindowTemplateType windowTemplateType)
        {
            switch (windowTemplateType)
            {
                case WindowTemplateType.AoiParam:
                    if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql) SaveAoi2DB(AoiParams);
                    else SaveDefault(FileNameAoiParams, AoiParams, IsOldAoiParams);
                    break;
                case WindowTemplateType.Calibration:
                    SaveDefault(FileNameCalibrationParams, CalibrationParams, IsOldCalibrationParams);
                    break;
                case WindowTemplateType.PGParam:
                    SaveDefault(FileNamePGParams, PGParams, IsOldPGParams);
                    break;
                case WindowTemplateType.LedReuslt:
                    SaveDefault(FileNameLedJudgeParams, LedReusltParams, IsOldLedJudgeParams);
                    break;
                case WindowTemplateType.SxParm:
                    SaveDefault(FileNameSxParms, SxParams, IsOldSxParams);
                    break;
                case WindowTemplateType.PoiParam:
                    if (!GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
                        SaveDefault(FileNamePoiParms, PoiParams, IsOldPoiParams);
                    break;
                case WindowTemplateType.FlowParam:
                    SaveDefault(FileNameFlowParms, FlowParams, IsOldFlowParams);
                    break;
                default:
                    break;
            }
        }

        public void SavePOI2DB(PoiParam poiParam)
        {
            poiService.Save(poiParam);
        }

        private static void SaveDefault<T>(string FileNameParams, ObservableCollection<KeyValuePair<string, T>> t, bool IsOldParams)
        {
            if (IsOldParams)
                CfgFile.Save(FileNameParams, ObservableCollectionToDictionary(t));
            else
                CfgFile.Save(FileNameParams, t);
        }

        private static Dictionary<string,T> ObservableCollectionToDictionary<T>(ObservableCollection<KeyValuePair<string, T>> keyValues)
        {
            var keys = new Dictionary<string, T>() { };
            foreach (var key in keyValues)
            {
                keys.Add(key.Key, key.Value);
            }
            return keys;
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
                    PoiParams = IDefault(FileNamePoiParms, new PoiParam(), ref IsOldPoiParams);
            }

            return PoiParams;
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
        internal AoiParam? AddAoiParam(string text)
        {
            ModMasterModel flowMaster = new ModMasterModel(ModMasterType.Aoi, text, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
            modService.Save(flowMaster);
            int pkId = flowMaster.GetPK();
            if (pkId > 0)
            {
                return LoadAoiParamById(pkId);
            }
            return null;
        }
    
        internal PoiParam? AddPoiParam(string text)
        {
            PoiMasterModel poiMaster = new PoiMasterModel(text, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
            poiService.Save(poiMaster);
            int pkId = poiMaster.GetPK();
            if (pkId > 0 )
            {
               return LoadPoiParamById(pkId);
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

        private AoiParam? LoadAoiParamById(int pkId)
        {
            ModMasterModel aoiMaster = modService.GetMasterById(pkId);
            List<ModDetailModel> aoiDetail = modService.GetDetailByPid(pkId);
            if (aoiMaster != null) return new AoiParam(aoiMaster, aoiDetail);
            else return null;
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
                    FlowParams.Add(item);
                }
            }
            else
            {
                FlowParams = IDefault(FileNameFlowParms, new FlowParam(), ref IsOldFlowParams);
            }
            return FlowParams;
        }


        internal ObservableCollection<KeyValuePair<string, AoiParam>> LoadAoiParam()
        {
            AoiParams.Clear();
            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
            {
                List<ModMasterModel> flows = modService.GetAoiAll(GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
                foreach (var dbModel in flows)
                {
                    List<ModDetailModel> flowDetails = modService.GetDetailByPid(dbModel.Id);
                    KeyValuePair<string, AoiParam> item = new KeyValuePair<string, AoiParam>(dbModel.Name ?? "default", new AoiParam(dbModel, flowDetails));
                    AoiParams.Add(item);
                }
            }
            else
            {
                AoiParam param = new AoiParam
                {
                    FilterByArea = true,
                    MaxArea = 6000,
                    MinArea = 10,
                    FilterByContrast = true,
                    MaxContrast = 1.7f,
                    MinContrast = 0.3f,
                    ContrastBrightness = 1.0f,
                    ContrastDarkness = 0.5f,
                    BlurSize = 19,
                    MinContourSize = 5,
                    ErodeSize = 5,
                    DilateSize = 5,
                    Left = 5,
                    Right = 5,
                    Top = 5,
                    Bottom = 5
                };
                AoiParams = IDefault(FileNameAoiParams, param, ref IsOldAoiParams);
            }
            return AoiParams;
        }

        internal int PoiMasterDeleteById(int id)
        {
            return poiService.MasterDeleteById(id);
        }

        internal int ModMasterDeleteById(int id)
        {
           return modService.MasterDeleteById(id);
        }

        internal void SaveAoi2DB(ObservableCollection<KeyValuePair<string, AoiParam>> aoiParams)
        {
            foreach (var item in aoiParams)
            {
                SaveAoi2DB(item.Value);
            }
        }
        internal void SaveAoi2DB(AoiParam aoiParam)
        {
            modService.Save(aoiParam);
        }
        internal void SaveFlow2DB(FlowParam flowParam)
        {
            modService.Save(flowParam);
        }

        public ObservableCollection<KeyValuePair<string, AoiParam>> AoiParams { get; set; }
        public ObservableCollection<KeyValuePair<string, CalibrationParam>> CalibrationParams { get; set; } 
        public ObservableCollection<KeyValuePair<string, PGParam>> PGParams { get; set; }
        public ObservableCollection<KeyValuePair<string, SxParam>> SxParams { get; set; }
        public ObservableCollection<KeyValuePair<string, LedReusltParam>> LedReusltParams { get; set; }
        public ObservableCollection<KeyValuePair<string, PoiParam>> PoiParams { get; set; }
        public ObservableCollection<KeyValuePair<string, FlowParam>> FlowParams { get; set; }
    }
}
