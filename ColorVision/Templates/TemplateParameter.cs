//using System;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using ColorVision.MySql.DAO;
//using ColorVision.MySql.Service;
//using cvColorVision.Util;
//using cvColorVision;
//using ColorVision.Solution;

//namespace ColorVision.Templates
//{
//    public class TemplateParameterPOI : TemplateParameter<PoiParam>
//    {
//        private PoiService PoiService = new PoiService();

//        public TemplateParameterPOI() : base()
//        {
//            Params = new ObservableCollection<TemplateModel<PoiParam>>();
//        }

//        public override ObservableCollection<TemplateModel<PoiParam>> Load()
//        {
//            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
//            {
//                Params.Clear();
//                List<PoiMasterModel> poiMaster = PoiService.GetMasterAll(GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
//                foreach (var dbModel in poiMaster)
//                {
//                    Params.Add(new TemplateModel<PoiParam>(dbModel.Name ?? "default", new PoiParam(dbModel)));
//                }
//            }
//            else
//            {
//                Params.Clear();
//                if (Params.Count == 0)
//                    Params = IDefault($"{ModMasterType.POI}.cfg", new PoiParam());
//            }

//            return Params;
//        }

//        internal PoiParam? AddPoiParam(string text)
//        {
//            PoiMasterModel poiMaster = new PoiMasterModel(text, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
//            PoiService.Save(poiMaster);
//            int pkId = poiMaster.GetPK();
//            if (pkId > 0)
//            {
//                return LoadPoiParamById(pkId);
//            }
//            return null;
//        }
//        internal PoiParam? LoadPoiParamById(int pkId)
//        {
//            PoiMasterModel poiMaster = PoiService.GetMasterById(pkId);
//            if (poiMaster != null) return new PoiParam(poiMaster);
//            else return null;
//        }

//        internal void LoadPoiDetailFromDB(PoiParam poiParam)
//        {
//            poiParam.PoiPoints.Clear();


//            List<PoiDetailModel> poiDetail = PoiService.GetDetailByPid(poiParam.ID);
//            foreach (var dbModel in poiDetail)
//            {
//                poiParam.PoiPoints.Add(new PoiParamData(dbModel));
//            }
//        }

//        public override void Save()
//        {

//            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
//            {
//                foreach (var item in Params)
//                {

//                    var modMasterModel = PoiService.GetMasterById(item.ID);
//                    if (modMasterModel != null)
//                    {
//                        modMasterModel.Name = item.Key;
//                        PoiService.Save(modMasterModel);
//                    }
//                }
//            }
//            else
//            {
//            }
//        }


//    }



//    public class TemplateParameter<T> : TemplateParameter where T : ParamBase, new()
//    {
//        public ObservableCollection<TemplateModel<T>> Params { get; set; }

//        string TemplatePath { get => SolutionManager.GetInstance().SolutionDirectory.FullName + "\\CFG\\" + cfgFile + ".cfg") }

//        public TemplateParameter()
//        {
//            Params = Load();
//        }

//        public T? LoadCFG<T>()
//        {
//            return CfgFile.Load<T>(TemplatePath);
//        }

//        private void SaveCFG<T>() where T : ParamBase
//        {
//            CfgFile.Save(TemplatePath, Params);
//        }

//        public ObservableCollection<TemplateModel<T>> IDefault<T>(string FileName, T Default) where T : ParamBase
//        {
//            ObservableCollection<TemplateModel<T>> Params = new ObservableCollection<TemplateModel<T>>();

//            Params = LoadCFG<T>();
//            if (Params.Count == 0)
//            {
//                Params.Add(new TemplateModel<T>("default", Default));
//            }

//            foreach (var item in Params)
//            {
//                item.Value.IsEnabledChanged += (s, e) =>
//                {
//                    foreach (var item2 in Params)
//                    {
//                        if (item2.Key != item.Key)
//                            item2.Value.IsEnable = false;
//                    }
//                };
//            }
//            Params.CollectionChanged += (s, e) =>
//            {
//                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
//                {
//                    Params[e.NewStartingIndex].Value.IsEnabledChanged += (s, e1) =>
//                    {
//                        foreach (var item2 in Params)
//                        {
//                            if (item2.Key != Params[e.NewStartingIndex].Key)
//                                item2.Value.IsEnable = false;
//                        }
//                    };

//                }
//            };
//            return Params;
//        }


//        public virtual ObservableCollection<TemplateModel<T>> Load()
//        {
//            return Params ?? new ObservableCollection<TemplateModel<T>>();
//        }

//    }



//    public class TemplateParameter
//    {


//        public virtual void Save()
//        {
//        }
//        public virtual void Create()
//        {
//        }
//        public virtual void Open()
//        {

//        }
//    }
//}
