namespace ColorVision.Engine.Templates.POI
{
    /// <summary>
    /// ColorVision 内部统一使用的 POI 形状。
    /// 数值保持与历史模板中的 GraphicTypes 一致，避免旧模板反序列化后变形。
    /// </summary>
    public enum PoiShape
    {
        None = -99,
        LegacySolidPoint = -1,
        Circle = 0,
        Rect = 1,
        Quadrilateral = 2,
        Point = 3,
        Polygon = 4,
        LeftTopRect = 5
    }
}
