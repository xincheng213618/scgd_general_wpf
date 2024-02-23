using ColorVision.Services.Templates.POI.Dao;

namespace ColorVision.Services.Templates.POI
{
    public class PoiParamData
    {
        public PoiParamData(PoiDetailModel dbModel)
        {
            ID = dbModel.Id;
            Name = dbModel.Name ?? dbModel.Id.ToString();
            PointType = dbModel.Type switch
            {
                0 => RiPointTypes.Circle,
                1 => RiPointTypes.Rect,
                2 => RiPointTypes.Mask,
                _ => RiPointTypes.Circle,
            };
            PixX = dbModel.PixX ?? 0;
            PixY = dbModel.PixY ?? 0;
            PixWidth = dbModel.PixWidth ?? 0;
            PixHeight = dbModel.PixHeight ?? 0;
        }

        public PoiParamData()
        {
        }

        public int ID { set; get; }

        public string Name { set; get; }
        public RiPointTypes PointType { set; get; }
        public double PixX { set; get; }
        public double PixY { set; get; }
        public double PixWidth { set; get; }
        public double PixHeight { set; get; }
    }

}
