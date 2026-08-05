namespace ColorVision.Engine.Templates.POI
{
    /// <summary>
    /// ColorVision 自有的 POI 数据模型。外部算法和流程协议类型应在边界处转换为此类型。
    /// </summary>
    public class PoiPoint
    {
        public PoiPoint(PoiDetailModel dbModel)
        {
            Id = dbModel.Id;
            Name = dbModel.Name ?? dbModel.Id.ToString();
            PointType = dbModel.Type.ToPoiShape();
            PixX = dbModel.PixX ?? 0;
            PixY = dbModel.PixY ?? 0;
            PixWidth = dbModel.PixWidth ?? 0;
            PixHeight = dbModel.PixHeight ?? 0;
        }

        public PoiPoint()
        {
        }

        public PoiPoint(int? id, int pid, string? name, PoiShape pointType, double pixelX, double pixelY, double width, double height)
        {
            Id = id ?? -1;
            Pid = pid;
            Name = name ?? string.Empty;
            PointType = pointType;
            PixX = pixelX;
            PixY = pixelY;
            PixWidth = width;
            PixHeight = height;
        }

        public int Id { set; get; }
        public int Pid { set; get; } = -1;
        public string Name { set; get; } = string.Empty;
        public PoiShape PointType { set; get; }
        public double PixX { set; get; }
        public double PixY { set; get; }
        public double PixWidth { set; get; }
        public double PixHeight { set; get; }

        // Result JSON and older callers use these names. Keep them as aliases while
        // the canonical storage remains the existing Pix* template contract.
        public double PixelX { get => PixX; set => PixX = value; }
        public double PixelY { get => PixY; set => PixY = value; }
        public double Width { get => PixWidth; set => PixWidth = value; }
        public double Height { get => PixHeight; set => PixHeight = value; }
        public double Radius => PixWidth / 2;
    }
}
