using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Templates.POI.Dao;
using ColorVision.Settings;
using ColorVision.UI;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Templates.POI
{
    public class PoiParamMenuItem : IMenuItem
    {
        public string? OwnerGuid => "Template";

        public string? GuidId => "PoiParam";
        public int Order => 1;
        public string? Header => "关注点模板设置(_P)";

        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new RelayCommand(a => {
            SoftwareConfig SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
            if (SoftwareConfig.IsUseMySql && !SoftwareConfig.MySqlControl.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(TemplateType.PoiParam) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });

    }



    /// <summary>
    /// 关注点模板
    /// </summary>
    public class PoiParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<PoiParam>> Params { get; set; } = new ObservableCollection<TemplateModel<PoiParam>>();

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
