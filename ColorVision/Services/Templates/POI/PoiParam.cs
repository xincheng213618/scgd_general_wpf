using ColorVision.Common.MVVM;
using ColorVision.Common.Sorts;
using ColorVision.Common.Utilities;
using ColorVision.Services.Dao;
using ColorVision.Services.Flow;
using ColorVision.Services.Templates.POI.Dao;
using ColorVision.Settings;
using ColorVision.UI;
using Newtonsoft.Json;
using NPOI.SS.Formula.Functions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Templates.POI
{
    public class ExportPoiParam : IMenuItem
    {
        public string? OwnerGuid => "Template";

        public string? GuidId => "PoiParam";
        public int Order => 1;
        public string? Header => ColorVision.Properties.Resource.MenuPoi;

        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new RelayCommand(a => {
            SoftwareConfig SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
            if (SoftwareConfig.IsUseMySql && !SoftwareConfig.MySqlControl.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(new TemplatePOI()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });

    }

    public class TemplatePOI : ITemplate<PoiParam>
    {
        public TemplatePOI()
        {
            Title = "关注点设置";
            Code = ModMasterType.POI;
            TemplateParams = PoiParam.Params;
        }
        public override void PreviewMouseDoubleClick(int index)
        {
            var WindowFocusPoint = new WindowFocusPoint(PoiParam.Params[index].Value) { Owner = Application.Current.GetActiveWindow() };
            WindowFocusPoint.ShowDialog();
        }

        public override void Load() => PoiParam.LoadPoiParam();

        public override void Save()
        {
            foreach (var item in TemplateParams)
            {
                var modMasterModel = PoiMasterDao.Instance.GetById(item.Id);
                if (modMasterModel != null)
                {
                    modMasterModel.Name = item.Key;
                    PoiMasterDao.Instance.Save(modMasterModel);
                }
            }
        }
        public override void Delete(int index)
        {
            PoiMasterDao poiMasterDao = new PoiMasterDao();
            poiMasterDao.DeleteById(TemplateParams[index].Value.Id);
            TemplateParams.RemoveAt(index);
        }

        public override void Create(string templateName)
        {
            PoiParam? param = PoiParam.AddPoiParam(templateName);
            if (param != null)
            {
                var a = new TemplateModel<PoiParam>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(T)}模板失败", "ColorVision");
            }
        }
    }

    /// <summary>
    /// 关注点模板
    /// </summary>
    public class PoiParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<PoiParam>> Params { get; set; } = new ObservableCollection<TemplateModel<PoiParam>>();
        public static void Save2DB(PoiParam poiParam)
        {
            PoiMasterModel poiMasterModel = new PoiMasterModel(poiParam);
            PoiMasterDao.Instance.Save(poiMasterModel);

            List<PoiDetailModel> poiDetails = new List<PoiDetailModel>();
            foreach (PoiParamData pt in poiParam.PoiPoints)
            {
                PoiDetailModel poiDetail = new PoiDetailModel(poiParam.Id, pt);
                poiDetails.Add(poiDetail);
            }
            PoiDetailDao.Instance.SaveByPid(poiParam.Id, poiDetails);
        }

        public static ObservableCollection<TemplateModel<PoiParam>> LoadPoiParam()
        {
            PoiParam.Params.Clear();
            List<PoiMasterModel> poiMasters = PoiMasterDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "tenant_id", ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId },{ "is_delete", 0 } });
            foreach (var dbModel in poiMasters)
            {
                PoiParam.Params.Add(new TemplateModel<PoiParam>(dbModel.Name ?? "default", new PoiParam(dbModel)));
            }
            return PoiParam.Params;
        }

        public static void LoadPoiDetailFromDB(PoiParam poiParam)
        {
            poiParam.PoiPoints.Clear();
            List<PoiDetailModel> poiDetails = PoiDetailDao.Instance.GetAllByPid(poiParam.Id);
            foreach (var dbModel in poiDetails)
            {
                poiParam.PoiPoints.AddUnique(new PoiParamData(dbModel));
            }
        }


        public static PoiParam? AddPoiParam(string TemplateName)
        {
            PoiMasterModel poiMasterModel = new PoiMasterModel(TemplateName, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            PoiMasterDao.Instance.Save(poiMasterModel);

            int pkId = poiMasterModel.Id;
            if (pkId > 0)
            {
                PoiMasterModel Service = PoiMasterDao.Instance.GetById(pkId);
                if (Service != null) return new PoiParam(Service);
                else return null;
            }
            return null;
        }

        public PoiParam()
        {
            Id = No++;
        }

        public PoiParam(PoiMasterModel dbModel)
        {
            Id = dbModel.Id;

            PoiName = dbModel.Name ?? string.Empty;
            Width = dbModel.Width ?? 0;
            Height = dbModel.Height ?? 0;
            Type = dbModel.Type ?? 0;
            DatumArea.X1X = dbModel.LeftTopX ?? 0;
            DatumArea.X1Y = dbModel.LeftTopY ?? 0;
            DatumArea.X2X = dbModel.RightTopX ?? 0;
            DatumArea.X2Y = dbModel.RightTopY ?? 0;
            DatumArea.X3X = dbModel.RightBottomX ?? 0;
            DatumArea.X3Y = dbModel.RightBottomY ?? 0;
            DatumArea.X4X = dbModel.LeftBottomX ?? 0;
            DatumArea.X4Y = dbModel.LeftBottomY ?? 0;
            DatumArea.CenterX = (DatumArea.X2X - DatumArea.X1X) / 2;
            DatumArea.CenterY = (DatumArea.X4Y - DatumArea.X1Y) / 2;
            CfgJson = dbModel.CfgJson ?? string.Empty;
        }

        public string CfgJson
        {
            get => JsonConvert.SerializeObject(DatumArea);
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    DatumArea ??= new DatumArea();
                }
                else
                {
                    try
                    {
                        DatumArea = JsonConvert.DeserializeObject<DatumArea>(value) ?? new DatumArea();
                    }
                    catch 
                    {
                        DatumArea = new DatumArea();
                    }
                }
            }
        }


        public string PoiName { get { return _PoiName; } set { _PoiName = value; NotifyPropertyChanged(); } }
        private string _PoiName;

        public int Type { get => _Type; set { _Type = value; NotifyPropertyChanged(); } }
        private int _Type;


        public int Width { get => _Width; set { _Width = value; NotifyPropertyChanged(); } }
        private int _Width;

        public int Height { get => _Height; set { _Height = value; NotifyPropertyChanged(); } }
        private int _Height;


        /// <summary>
        /// 关注点列表
        /// </summary>
        public ObservableCollection<PoiParamData> PoiPoints { get; set; } = new ObservableCollection<PoiParamData>();

        public DatumArea DatumArea { get; set; } = new DatumArea();




        [JsonIgnore]
        public bool IsPointCircle { get => DefaultPointType == RiPointTypes.Circle; set { if (value) DefaultPointType = RiPointTypes.Circle; NotifyPropertyChanged(); } }
        [JsonIgnore]
        public bool IsPointRect { get => DefaultPointType == RiPointTypes.Rect; set { if (value) DefaultPointType = RiPointTypes.Rect; NotifyPropertyChanged(); } }
        [JsonIgnore]
        public bool IsPointMask { get => DefaultPointType == RiPointTypes.Mask; set { if (value) DefaultPointType = RiPointTypes.Rect; NotifyPropertyChanged(); } }
        public RiPointTypes DefaultPointType { set; get; }

    }

}
