#pragma warning disable CS8625,CS8604,CS8602
using ColorVision.UI;

namespace ColorVision.Engine.Templates.POI
{
    public class PoiEditRectCache:IConfig
    {
        public static PoiEditRectCache Instance => ConfigService.Instance.GetRequiredService<PoiEditRectCache>();
        public int? LeftTopX { get; set; }
        public int? LeftTopY { get; set; }
        public int? RightTopX { get; set; }
        public int? RightTopY { get; set; }
        public int? RightBottomX { get; set; }
        public int? RightBottomY { get; set; }
        public int? LeftBottomX { get; set; }
        public int? LeftBottomY { get; set; }
    }

}
