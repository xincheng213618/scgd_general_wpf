using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Templates.POI
{
    public static class PoiParamExtension
    {

        public static int Save2DB(this PoiParam poiParam)
        {
            (poiParam.Storage ?? PoiTemplateStorage.Default).Save(poiParam);
            return 1;
        }
    }

    /// <summary>
    /// 关注点模板
    /// </summary>
    public class PoiParam : ParamModBase
    {
        [JsonIgnore]
        internal PoiTemplateStorage? Storage { get; set; }
        [JsonIgnore]
        internal bool DetailsLoaded { get; set; }

        public static void LoadPoiDetailFromDB(PoiParam poiParam)
        {
            var points = (poiParam.Storage ?? PoiTemplateStorage.Default).ReadPoints(poiParam.Id);
            poiParam.PoiPoints.Clear();
            foreach (PoiPoint point in points) poiParam.PoiPoints.Add(point);
            poiParam.DetailsLoaded = true;
        }

        public static Task<List<PoiPoint>> LoadPoiDetailsFromDBAsync(int poiId, CancellationToken cancellationToken = default)
            => Task.Run(() => PoiTemplateStorage.Default.ReadPoints(poiId), cancellationToken);

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

            CfgJson = dbModel.CfgJson ?? string.Empty;
            PoiConfig.IsPoiCIEFile = dbModel.IsDynamics ??false;

            LeftTopX = dbModel.LeftTopX;
            LeftTopY = dbModel.LeftTopY;
            RightTopX = dbModel.RightTopX;
            RightTopY = dbModel.RightTopY;
            RightBottomX = dbModel.RightBottomX;
            RightBottomY = dbModel.RightBottomY;
            LeftBottomX = dbModel.LeftBottomX;
            LeftBottomY = dbModel.LeftBottomY;
        }

        public int? LeftTopX { get => _LeftTopX; set { _LeftTopX = value; OnPropertyChanged(); } }
        private int? _LeftTopX;
        public int? LeftTopY { get => _LeftTopY; set { _LeftTopY = value; OnPropertyChanged(); } }
        private int? _LeftTopY;
        public int? RightTopX { get => _RightTopX; set { _RightTopX = value; OnPropertyChanged(); } }
        private int? _RightTopX;
        public int? RightTopY { get => _RightTopY; set { _RightTopY = value; OnPropertyChanged(); } }
        private int? _RightTopY;
        public int? RightBottomX { get => _RightBottomX; set { _RightBottomX = value; OnPropertyChanged(); } }
        private int? _RightBottomX;
        public int? RightBottomY { get => _RightBottomY; set { _RightBottomY = value; OnPropertyChanged(); } }
        private int? _RightBottomY;
        public int? LeftBottomX { get => _LeftBottomX; set { _LeftBottomX = value; OnPropertyChanged(); } }
        private int? _LeftBottomX;
        public int? LeftBottomY { get => _LeftBottomY; set { _LeftBottomY = value; OnPropertyChanged(); } }
        private int? _LeftBottomY;


        public string CfgJson
        {
            get => JsonConvert.SerializeObject(PoiConfig);
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    PoiConfig ??= new PoiConfig();
                }
                else
                {
                    try
                    {
                        PoiConfig = JsonConvert.DeserializeObject<PoiConfig>(value) ?? new PoiConfig();
                    }
                    catch
                    {
                        PoiConfig = new PoiConfig();
                    }
                }
            }
        }

        public int Type { get => _Type; set { _Type = value; OnPropertyChanged(); } }
        private int _Type;


        public int Width { get => _Width; set { _Width = value; OnPropertyChanged(); } }
        private int _Width;

        public int Height { get => _Height; set { _Height = value; OnPropertyChanged(); } }
        private int _Height;







        /// <summary>
        /// 关注点列表
        /// </summary>
        public ObservableCollection<PoiPoint> PoiPoints { get; set; } = new ObservableCollection<PoiPoint>();

        public PoiConfig PoiConfig { get; set; } = new PoiConfig();


    }

}
