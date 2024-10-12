using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Templates.POI.Comply;
using ColorVision.Engine.Templates.POI.Dao;
using ColorVision.UI.Sorts;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Engine.Templates.POI
{
    /// <summary>
    /// 关注点模板
    /// </summary>
    public class PoiParam : ParamBase
    {
        public static void Save2DB(PoiParam poiParam)
        {
            PoiMasterModel poiMasterModel = new(poiParam);
            PoiMasterDao.Instance.Save(poiMasterModel);

            List<PoiDetailModel> poiDetails = new();
            foreach (PoiPoint pt in poiParam.PoiPoints)
            {
                PoiDetailModel poiDetail = new PoiDetailModel(poiParam.Id, pt);
                poiDetails.Add(poiDetail);
            }
            PoiDetailDao.Instance.SaveByPid(poiParam.Id, poiDetails);
        }
          

        public static void LoadPoiDetailFromDB(PoiParam poiParam)
        {
            poiParam.PoiPoints.Clear();
            List<PoiDetailModel> poiDetails = PoiDetailDao.Instance.GetAllByPid(poiParam.Id);
            foreach (var dbModel in poiDetails)
            {
                poiParam.PoiPoints.Add(new PoiPoint(dbModel));
            }
        }

        public PoiParam()
        {

        }

        public PoiParam(PoiMasterModel dbModel)
        {
            Id = dbModel.Id;

            Name = dbModel.Name ?? string.Empty;
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
            ValidateId = dbModel.ValidateId ?? -1;
            DatumArea.IsPoiCIEFile = dbModel.IsDynamics ??false;
        }

        public string CfgJson
        {
            get => JsonConvert.SerializeObject(DatumArea);
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    DatumArea ??= new PoiConfig();
                }
                else
                {
                    try
                    {
                        DatumArea = JsonConvert.DeserializeObject<PoiConfig>(value) ?? new PoiConfig();
                    }
                    catch
                    {
                        DatumArea = new PoiConfig();
                    }
                }
            }
        }

        public int Type { get => _Type; set { _Type = value; NotifyPropertyChanged(); } }
        private int _Type;


        public int Width { get => _Width; set { _Width = value; NotifyPropertyChanged(); } }
        private int _Width;

        public int Height { get => _Height; set { _Height = value; NotifyPropertyChanged(); } }
        private int _Height;
        public int ValidateId { get => _ValidateId; set { _ValidateId = value; NotifyPropertyChanged(); } }
        private int _ValidateId;

        public RelayCommand ValidateCIEAVGCommand => new RelayCommand(a =>
        {
            var Template = new TemplateComplyParam("Comply.CIE.AVG");
            new WindowTemplate(Template, Template.FindIndex(ValidateId)) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });

        /// <summary>
        /// 关注点列表
        /// </summary>
        public ObservableCollection<PoiPoint> PoiPoints { get; set; } = new ObservableCollection<PoiPoint>();

        public PoiConfig DatumArea { get; set; } = new PoiConfig();

        [JsonIgnore]
        public bool IsPointCircle { get => DefaultPointType == RiPointTypes.Circle; set { if (value) DefaultPointType = RiPointTypes.Circle; NotifyPropertyChanged(); } }
        [JsonIgnore]
        public bool IsPointRect { get => DefaultPointType == RiPointTypes.Rect; set { if (value) DefaultPointType = RiPointTypes.Rect; NotifyPropertyChanged(); } }
        [JsonIgnore]
        public bool IsPointMask { get => DefaultPointType == RiPointTypes.Mask; set { if (value) DefaultPointType = RiPointTypes.Rect; NotifyPropertyChanged(); } }
        public RiPointTypes DefaultPointType { set; get; }

    }

}
