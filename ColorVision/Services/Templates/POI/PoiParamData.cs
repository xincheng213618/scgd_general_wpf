using ColorVision.Common.Sorts;
using ColorVision.Services.Templates.POI.Dao;

namespace ColorVision.Services.Templates.POI
{
    public class PoiParamData :ISortID
    {
        public PoiParamData(PoiDetailModel dbModel)
        {
            Id = dbModel.Id;
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

        public int Id { set; get; }

        public string Name { set; get; }
        public RiPointTypes PointType { set; get; }
        public double PixX { set; get; }
        public double PixY { set; get; }
        public double PixWidth { set; get; }
        public double PixHeight { set; get; }

        public object Tag { set; get; }

        public int IdShow { get; set; }
    }

}
