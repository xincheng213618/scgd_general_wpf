using ColorVision.Templates.POI.MySql;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ColorVision.Templates
{
    /// <summary>
    /// 关注点模板
    /// </summary>
    public class PoiParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<PoiParam>> Params { get; set; }

        public PoiParam()
        {
            this.Id = No++;
        }

        public PoiParam(PoiMasterModel dbModel)
        {
            this.Id = dbModel.Id;

            this.PoiName = dbModel.Name ?? string.Empty;
            this.Width = dbModel.Width ?? 0;
            this.Height = dbModel.Height ?? 0;
            this.Type = dbModel.Type ?? 0;
            this.DatumArea.X1X = dbModel.LeftTopX ?? 0;
            this.DatumArea.X1Y = dbModel.LeftTopY ?? 0;
            this.DatumArea.X2X = dbModel.RightTopX ?? 0;
            this.DatumArea.X2Y = dbModel.RightTopY ?? 0;
            this.DatumArea.X3X = dbModel.RightBottomX ?? 0;
            this.DatumArea.X3Y = dbModel.RightBottomY ?? 0;
            this.DatumArea.X4X = dbModel.LeftBottomX ?? 0;
            this.DatumArea.X4Y = dbModel.LeftBottomY ?? 0;
            this.DatumArea.CenterX = (this.DatumArea.X2X - this.DatumArea.X1X)/2;
            this.DatumArea.CenterY = (this.DatumArea.X4Y - this.DatumArea.X1Y) /2;
            this.CfgJson = dbModel.CfgJson ?? string.Empty;
        }

        public string CfgJson {
            get => JsonConvert.SerializeObject(DatumArea);
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    DatumArea ??= new DatumArea();
                }
                else
                {
                    DatumArea = JsonConvert.DeserializeObject<DatumArea>(value) ?? new DatumArea();
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
        public List<PoiParamData> PoiPoints { get; set; } = new List<PoiParamData>();

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
